using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using System.Collections.Generic;
using System.Linq;

namespace DizPlugin;

public class DizPlugin : BasePlugin
{
    public override string ModuleName => "Jailbreak: Diz Plugin";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "Sayrox";

    private static readonly string Prefix = $" {ChatColors.Default}[{ChatColors.Gold}Sayrox Diz{ChatColors.Default}]";
    
    private bool SetupInProgress = false;
    private CCSPlayerController? SetupPlayer = null;
    private Vector? SetupPoint1 = null;
    private Vector? SetupPoint2 = null;
    private bool DizIsActive = false;
    private Dictionary<int, bool> LockedPlayers = new();

    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnTick>(OnTick);
        RegisterEventHandler<EventBulletImpact>(OnBulletImpact);
        RegisterEventHandler<EventRoundEnd>((@ev, info) => { BreakDiz(); return HookResult.Continue; });
    }

    [ConsoleCommand("css_diz", "T oyunculari siralar")]
    public void OnDiz(CCSPlayerController? p, CommandInfo i)
    {
        if (p == null || !AdminManager.PlayerHasPermissions(p, "@css/generic")) return;
        if (DizIsActive) { p.PrintToChat($"{Prefix} {ChatColors.Red}Diz zaten aktif! Önce !dizboz kullanın."); return; }
        SetupInProgress = true; SetupPlayer = p; SetupPoint1 = null; SetupPoint2 = null;
        p.PrintToChat($"{Prefix} {ChatColors.Lime}Lütfen Birinci Noktaya Ateş Edin.");
    }

    [ConsoleCommand("css_dizboz", "Diziyi bozar")]
    public void OnDizBoz(CCSPlayerController? p, CommandInfo i)
    {
        if (p == null || !AdminManager.PlayerHasPermissions(p, "@css/generic")) return;
        BreakDiz();
        Server.PrintToChatAll($"{Prefix} {ChatColors.Lime}{p.PlayerName} {ChatColors.Default}diziyi bozdu.");
    }

    private HookResult OnBulletImpact(EventBulletImpact @event, GameEventInfo info)
    {
        var p = @event.Userid;
        if (!SetupInProgress || p == null || p != SetupPlayer) return HookResult.Continue;

        Vector impact = new(@event.X, @event.Y, @event.Z);
        if (SetupPoint1 == null)
        {
            SetupPoint1 = impact;
            p.PrintToChat($"{Prefix} {ChatColors.Lime}1. nokta alındı! Şimdi ikinci noktaya ateş edin.");
        }
        else if (SetupPoint2 == null)
        {
            SetupPoint2 = impact;
            SetupInProgress = false;
            StartDiz();
        }
        return HookResult.Continue;
    }

    private void StartDiz()
    {
        if (SetupPoint1 == null || SetupPoint2 == null) return;
        var ts = Utilities.GetPlayers().Where(x => x is { TeamNum: 2, PawnIsAlive: true }).ToList();
        if (ts.Count == 0) return;

        DizIsActive = true;
        LockedPlayers.Clear();

        float dx = SetupPoint2.X - SetupPoint1.X;
        float dy = SetupPoint2.Y - SetupPoint1.Y;
        float dz = SetupPoint2.Z - SetupPoint1.Z;

        for (int i = 0; i < ts.Count; i++)
        {
            float t = ts.Count == 1 ? 0.5f : (float)i / (ts.Count - 1);
            Vector target = new(SetupPoint1.X + dx * t, SetupPoint1.Y + dy * t, SetupPoint1.Z + 10);
            ts[i].PlayerPawn.Value?.Teleport(target, QAngle.Zero, Vector.Zero);
            var pawn = ts[i].PlayerPawn.Value;
            if (pawn != null) pawn.MoveType = MoveType_t.MOVETYPE_NONE;
            LockedPlayers[ts[i].Slot] = true;
        }
        Server.PrintToChatAll($"{Prefix} {ChatColors.Red}T takımı dizildi ve kilitlendi!");
    }

    private void OnTick()
    {
        if (!DizIsActive) return;
        foreach (var slot in LockedPlayers.Keys.ToList())
        {
            var p = Utilities.GetPlayerFromSlot(slot);
            if (p == null || !p.PawnIsAlive) { LockedPlayers.Remove(slot); continue; }
            var pawn = p.PlayerPawn.Value;
            if (pawn != null) { pawn.AbsVelocity.X = 0; pawn.AbsVelocity.Y = 0; }
        }
    }

    private void BreakDiz()
    {
        DizIsActive = false;
        SetupInProgress = false;
        foreach (var slot in LockedPlayers.Keys.ToList())
        {
            var p = Utilities.GetPlayerFromSlot(slot);
            if (p?.PlayerPawn.Value != null) p.PlayerPawn.Value.MoveType = MoveType_t.MOVETYPE_WALK;
        }
        LockedPlayers.Clear();
    }
}
