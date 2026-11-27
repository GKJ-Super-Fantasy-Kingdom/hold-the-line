param(
    [string]$GameDir = 'D:\SteamLibrary\steamapps\common\Super Fantasy Kingdom',
    [string]$ProfileDir = (Join-Path $PSScriptRoot '../.analysis/profile'),
    [string]$CopiedSave = (Join-Path $PSScriptRoot '../.analysis/saves/1_human_game.data'),
    [string]$NightsOfTerrorDll
)
# Run in a fresh PowerShell 7 process. All game objects here are managed-only fixtures.
$ErrorActionPreference = 'Stop'
$core = (Resolve-Path (Join-Path $ProfileDir 'BepInEx/core')).Path
$managed = Join-Path $GameDir 'Super Fantasy Kingdom_Data/Managed'
foreach ($dll in 'UnityEngine.CoreModule','UnityEngine','Assembly-CSharp') {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $managed "$dll.dll"))
}
foreach ($dll in 'Mono.Cecil','MonoMod.Utils','MonoMod.RuntimeDetour','0Harmony','BepInEx') {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $core "$dll.dll"))
}
$mod = [Reflection.Assembly]::LoadFrom((Join-Path $ProfileDir 'BepInEx/plugins/HoldTheLine.dll'))
$static = [Reflection.BindingFlags]'NonPublic,Static'
$instance = [Reflection.BindingFlags]'NonPublic,Instance'
function Assert($condition, $label) {
    if (!$condition) { throw "Failed: $label" }
    "PASS $label"
}

$wallType = [SuperFantasyKingdom.WallManager]
$wall = [Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject($wallType)
$hp = $wallType.GetField('m_Hitpoints',$instance)
$hp.SetValue($wall,[single]30)
$probe = $mod.GetType('HoldTheLine.WallHit')
$plugin = $mod.GetType('HoldTheLine.Plugin')
$current = $plugin.GetField('Current',$static)
$wager = [Activator]::CreateInstance($mod.GetType('HoldTheLine.Wager'),$true)
$copy = Get-Content -LiteralPath $CopiedSave -Raw | ConvertFrom-Json
$wager.Run = $copy.runID
$wager.Day = $copy.day + 1
$wager.State = 'Staked'
$current.SetValue($null,$wager)
# Run the packed transpiler on the HP store, then execute the transformed
# instruction sequence against a real managed WallManager without Unity VFX.
$instructions = [Collections.Generic.List[HarmonyLib.CodeInstruction]]::new()
$instructions.Add([HarmonyLib.CodeInstruction]::new([Reflection.Emit.OpCodes]::Stfld,$hp))
$patched = $probe.GetMethod('Transpiler',$static).Invoke($null,(,$instructions))
$store = [Reflection.Emit.DynamicMethod]::new('HitStore',[void],[type[]]@($wallType,[single]),$wallType,$true)
$il = $store.GetILGenerator()
$il.Emit([Reflection.Emit.OpCodes]::Ldarg_0)
$il.Emit([Reflection.Emit.OpCodes]::Ldarg_1)
foreach ($op in $patched) {
    if ($null -eq $op.operand) { $il.Emit($op.opcode) }
    else { $il.Emit($op.opcode,$op.operand) }
}
$il.Emit([Reflection.Emit.OpCodes]::Ret)
[void]$store.Invoke($null,@($wall,[single]30))
Assert ($hp.GetValue($wall) -eq 30 -and $wager.State -eq 'Staked') 'zero HP loss does not forfeit'
[void]$store.Invoke($null,@($wall,[single]24))
Assert ($hp.GetValue($wall) -eq 24 -and $wager.State -eq 'Lost') 'transformed native HP store records damage and writes original value'
$hp.SetValue($wall,[single]30)
Assert ($wager.State -eq 'Lost') 'full repair leaves recorded loss intact'

$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory($managed)
$resolver.AddSearchDirectory($core)
$reader = [Mono.Cecil.ReaderParameters]::new()
$reader.AssemblyResolver = $resolver
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $managed 'Assembly-CSharp.dll'),$reader)
$packaged = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($mod.Location,$reader)
function Body($type,$method) {
    $methodDef = $game.MainModule.GetType("SuperFantasyKingdom.$type").Methods | Where-Object Name -eq $method
    $methodDef.Body.Instructions
}
try {
    $refs = @($packaged.MainModule.GetMemberReferences() | Where-Object {
        $_.DeclaringType.Scope.Name -match '^(Unity|Assembly-CSharp|0Harmony|BepInEx|Newtonsoft)'
    })
    foreach ($ref in $refs) {
        if ($null -eq $ref.Resolve()) { throw "Unresolved reference $ref" }
    }
    Assert ($refs.Count -gt 0) "$($refs.Count) packaged runtime references resolve"
    $target = $mod.GetType('HoldTheLine.SaveHooks+Write').GetMethod('TargetMethod',$static).Invoke($null,@())
    $signature = $target.GetParameters().ParameterType.FullName -join ','
    Assert ($target.ReturnType -eq [void] -and $signature -in 'System.String,System.String','System.String,System.String,System.Boolean') 'save patch resolves a supported native overload'
    foreach ($type in 'SuperFantasyKingdom.GameDataManager','SuperFantasyKingdom.Tavern.TavernSaveManager') {
        $save = $game.MainModule.GetType($type).Methods | Where-Object Name -eq Save
        $calls = @($save.Body.Instructions | Where-Object {
            $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'Save' -and
            $_.Operand.DeclaringType.FullName -eq 'SuperFantasyKingdom.SaveManager'
        })
        Assert ($calls.Count -eq 1 -and ($calls[0].Operand.Parameters.ParameterType.FullName -join ',') -eq $signature) "$type reaches the selected save overload once"
        if ($signature.EndsWith(',System.Boolean')) {
            $expected = if ($type.EndsWith('.TavernSaveManager')) {'ldc.i4.1'} else {'ldc.i4.0'}
            Assert ($calls[0].Previous.OpCode.Name -eq $expected) "$type preserves its native backup policy"
        }
    }
    $hit = @(Body WallManager WallGetsHit)
    $writes = @($hit | Where-Object { $_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq 'm_Hitpoints' })
    Assert ($writes.Count -eq 1 -and $writes[0].Previous.Operand.Name -eq 'Max' -and
        $writes[0].Previous.Previous.OpCode.Name -eq 'sub') 'unique clamped subtraction after shield handling'
    $signal = $hit | Where-Object { $_.Operand.Name -eq 'OnWallDamageTaken' }
    Assert ($signal.Offset -gt $writes[0].Offset) 'native listeners observe damage after HP store'
    foreach ($route in 'GameManager/EndDay','DaytimeManager/SwitchState') {
        $parts = $route.Split('/')
        Assert ((Body $parts[0] $parts[1]).Operand.Name -contains 'ChangeState') "$route enters native state transition"
    }
    $transition = @(Body GameManager ChangeState)
    $event = $transition | Where-Object { $_.Operand.Name -eq 'BeforeStateChange' } | Select-Object -First 1
    $dayEnd = $transition | Where-Object { $_.Operand.Name -eq 'DayEnd' }
    Assert ($null -ne $event -and $event.Offset -lt $dayEnd.Offset -and
        (Body GameManager DayEnd).Operand.Name -contains 'Save') 'survival settlement precedes native checkpoint'
    Assert ((Body ResourceManager AddToCoins).Count -eq 7 -and
        (Body ResourceManager AddToCoins).Operand.Name -contains 'm_Coins') 'coin mutation is an observer-free field update'
    $attr = $packaged.MainModule.GetType('HoldTheLine.Plugin').CustomAttributes | Where-Object AttributeType -Match 'BepInPlugin'
    $listing = Get-Content (Join-Path $PSScriptRoot '../thunderstore/manifest.json') -Raw | ConvertFrom-Json
    Assert ($attr.ConstructorArguments[2].Value -eq $listing.version_number) 'packaged BepInEx version matches manifest'
} finally {
    $game.Dispose()
    $packaged.Dispose()
    $resolver.Dispose()
}

# Drive the packed Harmony callbacks in the game's actual read/write order. The
# two save files never leave memory. Simulate what Unity does with unknown fields.
function Hook($type,$name) {
    $mod.GetType("HoldTheLine.SaveHooks+$type").GetMethod($name,$static)
}
$raw = Hook RawRead Postfix
$write = Hook Write Prefix
$gameData = [Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject([SuperFantasyKingdom.GameData])
$gameData.runID = $copy.runID
$gameData.hash = $copy.hash
$wager.State = 'Staked'
$copy.coins -= 1
$wallet = [Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject([SuperFantasyKingdom.ResourceManager])
$wallet.SetCoins($copy.coins)
$credit = [SuperFantasyKingdom.ResourceManager].GetMethod('AddToCoins').CreateDelegate([Action[int]],$wallet)
$finish = $wager.GetType().GetMethod('Finish',$instance)
Assert ($finish.Invoke($wager,@($true,$credit))) 'one payout on successful transition'
$copy.coins = $wallet.GetCoins()
$saveArgs = [object[]]@('human_game',[string]($copy | ConvertTo-Json -Depth 100 -Compress))
[void]$write.Invoke($null,$saveArgs)
$night = $saveArgs[1]
Assert (($night | ConvertFrom-Json).gkjHoldTheLine.State -eq [int]$wager.State) 'world checkpoint includes paid receipt'
foreach ($route in 'TavernRead','WorldRead','WorldRead') {
    [void](Hook $route Prefix).Invoke($null,@())
    [void]$raw.Invoke($null,@('human_game','{'))
    [void]$raw.Invoke($null,@('human_game',$night))
    if ($route -eq 'TavernRead') {
        [void](Hook TavernData Postfix).Invoke($null,@($gameData))
    } else {
        [void](Hook WorldRead Postfix).Invoke($null,@($gameData))
    }
    [void](Hook $route Finalizer).Invoke($null,@())
    $loaded = $current.GetValue($null)
    $wallet.SetCoins(($night | ConvertFrom-Json).coins)
    Assert ($loaded.State -eq 'Paid' -and !$finish.Invoke($loaded,@($true,$credit)) -and
        $wallet.GetCoins() -eq $copy.coins) "$route restores selected backup without a second reward"
    if ($route -eq 'TavernRead') {
        $copy.day++
        $copy.isNight = $false
        $copy.hash = 'test-morning-hash'
        $gameData.hash = $copy.hash
        $saveArgs[1] = [string]($copy | ConvertTo-Json -Depth 100 -Compress)
        [void]$write.Invoke($null,$saveArgs)
        $night = $saveArgs[1]
    }
}

# Exercise the partner's actual reservation code without starting either Unity player.
if ($NightsOfTerrorDll) {
    $terror = [Reflection.Assembly]::LoadFrom((Resolve-Path $NightsOfTerrorDll).Path)
    $group = [Activator]::CreateInstance($terror.GetType('NightsOfTerror.Reinforcement'),$true)
    $clearance = $group.GetType().GetMethod('ForClearance',$instance)
    $arrived = $group.GetType().GetMethod('Arrived',$instance)
    $wager.State = 'Staked'
    $wallet.SetCoins(0)
    for ($i = 0; $i -lt 3; $i++) {
        Assert ($clearance.Invoke($group,@(0)) -gt 0 -and $wallet.GetCoins() -eq 0 -and $wager.State -eq 'Staked') "empty field before reinforcement $($i+1) still holds the wager"
        [void]$arrived.Invoke($group,@([single]$i,$true))
    }
    Assert ($clearance.Invoke($group,@(1)) -eq 1) 'last living reinforcement still blocks clearance'
    Assert ($clearance.Invoke($group,@(0)) -eq 0 -and $finish.Invoke($wager,@($true,$credit)) -and
        !$finish.Invoke($wager,@($true,$credit)) -and $wallet.GetCoins() -eq $wager.Payout) 'cleared group permits a single settlement'
    $partner = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($terror.Location)
    try {
        $guards = $partner.MainModule.GetType('NightsOfTerror.NightGates').NestedTypes
        foreach ($guardSpec in @(
            @{ Name='PhaseAdvance'; Gate='CountBeforeNight'; Signal='IsCombatOver' },
            @{ Name='TavernPrompt'; Gate='CountBeforeTavern'; Signal='ForClearance' }
        )) {
            $countMethod = [SuperFantasyKingdom.CombatManager].GetMethod('CurrentMonstersAmount')
            $site = [Collections.Generic.List[HarmonyLib.CodeInstruction]]::new()
            $site.Add([HarmonyLib.CodeInstruction]::new([Reflection.Emit.OpCodes]::Callvirt,$countMethod))
            $patch = $terror.GetType('NightsOfTerror.NightGates+'+$guardSpec.Name).GetMethod('Transpiler',$static)
            $output = @($patch.Invoke($null,(,$site)) | ForEach-Object { $_ })
            $gate = $partner.MainModule.GetType('NightsOfTerror.NightGates').Methods | Where-Object Name -eq $guardSpec.Gate
            Assert ($output.Count -eq 2 -and $output[0].operand -eq $countMethod -and
                $output[1].operand.Name -eq $guardSpec.Gate -and $gate.Body.Instructions.Operand.Name -contains $guardSpec.Signal) "$($guardSpec.Name) gates native clearance on $($guardSpec.Signal)"
        }
        $survival = $guards | Where-Object Name -eq 'Survival'
        $calls = ($survival.Methods | Where-Object Name -eq 'Prefix').Body.Instructions.Operand.Name
        Assert ($calls -contains 'IsCombatOver' -and $calls -contains 'BeginSettlement') 'partner guards native survival on combat completion before the wager event'
    } finally { $partner.Dispose() }
}
