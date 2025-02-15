#nullable enable

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Utils;
using System.Collections.Generic;
using System;
using System.Linq;

namespace KomutcuAdminPlugin
{
    public class KomutcuAdminInfo
    {
        public int Slot { get; set; }
        public string Name { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan TotalTime { get; set; }

        public KomutcuAdminInfo(int slot, string name)
        {
            Slot = slot;
            Name = name;
            StartTime = DateTime.Now;
            TotalTime = TimeSpan.Zero;
        }
    }

    [MinimumApiVersion(276)]
    public class KomutcuAdmin : BasePlugin
    {
        public override string ModuleName => "Komutçu Admin Plugin";
        public override string ModuleVersion => "1.0";
        public override string ModuleAuthor => "Alone Elxy";
        public override string ModuleDescription => "Komutçu admin belirleme eklentisi";

        private KomutcuAdminInfo? currentKomutcuAdmin = null;
        private Dictionary<string, TimeSpan> komutcuAdminHistory = new Dictionary<string, TimeSpan>();
        private CCSPlayerController? currentWarden = null;

        public override void Load(bool hotReload)
        {
            RegisterListener<Listeners.OnMapStart>(OnMapStart);
            RegisterListener<Listeners.OnTick>(() => OnTick());
            RegisterEventHandler<EventPlayerChat>(OnPlayerChat, HookMode.Pre);
            RegisterEventHandler<EventPlayerTeam>(OnPlayerTeam, HookMode.Pre);

            AddCommand("css_ka", "Komutçu admini belirler", CommandKomutcuAdmin);
            AddCommand("css_ka0", "Komutçu admini siler", CommandRemoveKomutcuAdmin);
            AddCommand("css_kasure", "Komutçu admin süresini gösterir", CommandKomutcuAdminTime);
            AddCommand("css_topka", "En fazla süreye sahip komutçu adminleri gösterir", CommandTopKomutcuAdmin);
            AddCommand("css_w", "Warden olmak için", CommandBecomeWarden);
            AddCommand("css_uw", "Warden'lıktan çıkmak için", CommandRemoveWarden);

            AddCommand("say !w", "Warden olmak için", CommandBecomeWarden);
            AddCommand("say_team !w", "Warden olmak için", CommandBecomeWarden);
            AddCommand("say !uw", "Warden'lıktan çıkmak için", CommandRemoveWarden);
            AddCommand("say_team !uw", "Warden'lıktan çıkmak için", CommandRemoveWarden);
            AddCommand("say !ka", "Komutçu admini belirler", CommandKomutcuAdmin);
            AddCommand("say_team !ka", "Komutçu admini belirler", CommandKomutcuAdmin);
            AddCommand("say !ka0", "Komutçu admini siler", CommandRemoveKomutcuAdmin);
            AddCommand("say_team !ka0", "Komutçu admini siler", CommandRemoveKomutcuAdmin);
            AddCommand("say !kasure", "Komutçu admin süresini gösterir", CommandKomutcuAdminTime);
            AddCommand("say_team !kasure", "Komutçu admin süresini gösterir", CommandKomutcuAdminTime);
            AddCommand("say !topka", "En fazla süreye sahip komutçu adminleri gösterir", CommandTopKomutcuAdmin);
            AddCommand("say_team !topka", "En fazla süreye sahip komutçu adminleri gösterir", CommandTopKomutcuAdmin);
        }

        private HookResult OnPlayerChat(EventPlayerChat @event, GameEventInfo info)
        {
            if (@event == null) return HookResult.Continue;

            var player = Utilities.GetPlayerFromUserid(@event.Userid);
            if (player == null || !player.IsValid) return HookResult.Continue;

            // Eğer konuşan kişi komutçu admin ise isminin başına tag ekle
            if (currentKomutcuAdmin != null && player.Slot == currentKomutcuAdmin.Slot)
            {
                string originalMessage = @event.Text;
                @event.Text = $" \x04[Komutçu Admin AE] \x01{originalMessage}";
                return HookResult.Changed;
            }

            return HookResult.Continue;
        }

        private HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
        {
            // Eğer oyuncu CT takımına katıldıysa
            if (@event.Team == 3)
            {
                var player = @event.Userid;
                if (player != null && player.IsValid && !AdminManager.PlayerHasPermissions(player, "@css/generic"))
                {
                    Server.PrintToChatAll($" \x02[Alone Elxy]{ChatColors.Green} {player.PlayerName} CT takımına katıldı. !w yazarak Warden olabilir!");
                }
            }

            if (currentWarden != null && @event.Userid == currentWarden)
            {
                currentWarden = null;
                Server.PrintToChatAll($" \x02[Alone Elxy]{ChatColors.Green} Warden takım değiştirdiği için kaldırıldı!");
            }
            return HookResult.Continue;
        }

        private void OnMapStart(string mapName)
        {
            if (currentKomutcuAdmin != null)
            {
                UpdateKomutcuAdminTime();
                currentKomutcuAdmin = null;
            }
            // Map değiştiğinde Warden'ı sıfırla
            currentWarden = null;
        }

        private void OnTick()
        {
            if (currentKomutcuAdmin != null)
            {
                var player = Utilities.GetPlayerFromSlot(currentKomutcuAdmin.Slot);
                if (player != null && player.IsValid)
                {
                    player.Clan = "[Komutçu Admin]";
                }
            }
        }

        private void UpdateKomutcuAdminTime()
        {
            if (currentKomutcuAdmin == null) return;

            var duration = DateTime.Now - currentKomutcuAdmin.StartTime;
            currentKomutcuAdmin.TotalTime += duration;

            if (komutcuAdminHistory.ContainsKey(currentKomutcuAdmin.Name))
            {
                komutcuAdminHistory[currentKomutcuAdmin.Name] += duration;
            }
            else
            {
                komutcuAdminHistory[currentKomutcuAdmin.Name] = duration;
            }
        }

        private void RemoveKomutcuAdminTag()
        {
            if (currentKomutcuAdmin != null)
            {
                var player = Utilities.GetPlayerFromSlot(currentKomutcuAdmin.Slot);
                if (player != null && player.IsValid)
                {
                    player.Clan = "";
                }
            }
        }

        private bool HasCommandAccess(CCSPlayerController? caller)
        {
            if (caller == null || !caller.IsValid) return false;

            // Eğer admin yetkisi varsa her durumda true
            if (AdminManager.PlayerHasPermissions(caller, "@css/generic")) return true;

            // Admin değilse ve Warden ise true
            if (currentWarden != null)
            {
                return caller.Slot == currentWarden.Slot;
            }

            return false;
        }

        private void CommandBecomeWarden(CCSPlayerController? caller, CommandInfo command)
        {
            if (caller == null || !caller.IsValid) return;

            // Admin kontrolü
            if (AdminManager.PlayerHasPermissions(caller, "@css/generic"))
            {
                caller.PrintToChat($" \x02[Alone Elxy]{ChatColors.Green} Adminler Warden olamaz!");
                return;
            }

            if (caller.TeamNum != 3)
            {
                caller.PrintToChat($" \x02[Alone Elxy]{ChatColors.Green} Sadece CT takımındaki oyuncular Warden olabilir!");
                return;
            }

            if (currentWarden != null)
            {
                caller.PrintToChat($" \x02[Alone Elxy]{ChatColors.Green} Zaten aktif bir Warden var: {currentWarden.PlayerName}");
                return;
            }

            currentWarden = caller;
            Server.PrintToChatAll($" \x02[Alone Elxy]{ChatColors.Green} {caller.PlayerName} artık Warden!");
        }

        private void CommandRemoveWarden(CCSPlayerController? caller, CommandInfo command)
        {
            if (caller == null || !caller.IsValid) return;

            if (currentWarden == null || caller.Slot != currentWarden.Slot)
            {
                caller.PrintToChat($" \x02[Alone Elxy]{ChatColors.Green} Sadece aktif Warden bu komutu kullanabilir!");
                return;
            }

            currentWarden = null;
            Server.PrintToChatAll($" \x02[Alone Elxy]{ChatColors.Green} {caller.PlayerName} artık Warden değil!");
        }

        private void CommandKomutcuAdmin(CCSPlayerController? caller, CommandInfo command)
        {
            if (caller == null || !caller.IsValid) return;

            // Yetki kontrolü
            if (!HasCommandAccess(caller))
            {
                caller.PrintToChat($" \x02[Alone Elxy]{ChatColors.Green} Bu komutu kullanma yetkiniz yok!");
                return;
            }

            string[] args = command.ArgString.Split(' ');
            if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                caller.PrintToChat($" \x02[Alone Elxy]{ChatColors.Green} Kullanım: !ka <oyuncu>");
                return;
            }

            // Hedef oyuncuyu bul
            string targetName = args[0].Trim();
            var players = Utilities.GetPlayers();
            var targetPlayer = players.FirstOrDefault(p =>
                p != null &&
                p.IsValid &&
                p.PlayerName != null &&
                p.PlayerName.IndexOf(targetName, StringComparison.OrdinalIgnoreCase) >= 0);

            if (targetPlayer == null)
            {
                caller.PrintToChat($" \x02[Alone Elxy]{ChatColors.Green} Oyuncu bulunamadı: {targetName}");
                return;
            }

            // Eğer zaten bir komutçu admin varsa, süresini kaydet ve tag'ini kaldır
            if (currentKomutcuAdmin != null)
            {
                UpdateKomutcuAdminTime();
                RemoveKomutcuAdminTag();
            }

            // Yeni komutçu admini ayarla
            currentKomutcuAdmin = new KomutcuAdminInfo(targetPlayer.Slot, targetPlayer.PlayerName);
            targetPlayer.Clan = "[Komutçu Admin]";
            Server.PrintToChatAll($" \x02[Alone Elxy]{ChatColors.Green} {targetPlayer.PlayerName} komutçu admin olarak belirlendi!");
        }

        private void CommandRemoveKomutcuAdmin(CCSPlayerController? caller, CommandInfo command)
        {
            if (caller == null || !caller.IsValid) return;

            // Yetki kontrolü
            if (!HasCommandAccess(caller))
            {
                caller.PrintToChat($" \x02[Alone Elxy]{ChatColors.Green} Bu komutu kullanma yetkiniz yok!");
                return;
            }

            if (currentKomutcuAdmin == null)
            {
                caller.PrintToChat($" \x02[Alone Elxy]{ChatColors.Green} Aktif komutçu admin bulunmuyor!");
                return;
            }

            var player = Utilities.GetPlayerFromSlot(currentKomutcuAdmin.Slot);
            RemoveKomutcuAdminTag(); // Tag'i kaldır
            UpdateKomutcuAdminTime();
            currentKomutcuAdmin = null;

            if (player != null && player.IsValid)
            {
                Server.PrintToChatAll($" \x02[Alone Elxy]{ChatColors.Green} {player.PlayerName} artık komutçu admin değil!");
            }
            else
            {
                Server.PrintToChatAll($" \x02[Alone Elxy]{ChatColors.Green} Komutçu admin kaldırıldı!");
            }
        }

        private void CommandKomutcuAdminTime(CCSPlayerController? caller, CommandInfo command)
        {
            if (caller == null || !caller.IsValid) return;

            if (currentKomutcuAdmin == null)
            {
                caller.PrintToChat($" \x02[Alone Elxy]{ChatColors.Green} Aktif komutçu admin bulunmuyor!");
                return;
            }

            var duration = DateTime.Now - currentKomutcuAdmin.StartTime;
            caller.PrintToChat($" \x02[Alone Elxy]{ChatColors.Green} Komutçu admin {currentKomutcuAdmin.Name}: {duration.TotalMinutes:F1} dakika");
        }

        private void CommandTopKomutcuAdmin(CCSPlayerController? caller, CommandInfo command)
        {
            if (caller == null || !caller.IsValid) return;

            if (komutcuAdminHistory.Count == 0)
            {
                caller.PrintToChat($" \x02[Alone Elxy]{ChatColors.Green} Henüz komutçu admin kaydı bulunmuyor!");
                return;
            }

            var topAdmins = komutcuAdminHistory
                .OrderByDescending(x => x.Value.TotalMinutes)
                .Take(5);

            caller.PrintToChat($" \x02[Alone Elxy]{ChatColors.Green} En fazla süreye sahip komutçu adminler:");
            int rank = 1;
            foreach (var admin in topAdmins)
            {
                caller.PrintToChat($" \x02[Alone Elxy]{ChatColors.Green} {rank}. {admin.Key}: {admin.Value.TotalMinutes:F1} dakika");
                rank++;
            }
        }

        private bool IsAdmin(CCSPlayerController player)
        {
            // TODO: Gerçek admin kontrolü eklenecek
            return true;
        }
    }
}

#nullable disable
