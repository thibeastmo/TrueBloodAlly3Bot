using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Logging;
namespace TrueBloodAlly3Bot {
    public class Bot {
        private const string Version = "1.0.0";

        //private const string Token = "";//TrueBloodAlly3Bot
        private const string Token = "";//War bot
        private static DiscordClient _discordClient;
        private BackgroundWorker _backgroundWorker;
        private bool _guildDownloadCompleted = false;

        public async Task RunAsync()
        {
            _discordClient = new DiscordClient(new DiscordConfiguration
            {
                Token = Token,
                TokenType = TokenType.Bot,
                AutoReconnect = true
            });

            await _discordClient.ConnectAsync();
            //Events
            _discordClient.MessageCreated += DiscordClient_MessageCreated;
            _discordClient.GuildDownloadCompleted += DiscordClient_GuildDownloadCompleted;
            _discordClient.MessageReactionAdded += DiscordClientOnMessageReactionAdded;
            _discordClient.MessageReactionRemoved += DiscordClientOnMessageReactionRemoved;

            StartBackgroundTask();

            await Task.Delay(-1);
        }

        #region Events

        private Task DiscordClient_MessageCreated(DiscordClient sender, MessageCreateEventArgs e)
        {
            _ = Task.Run(async () => {
                string content = await GetMessageContent(e.Channel, e.Message.Id);
                if (e.Channel.Id == Constants.Channels.BATTLE_LOGS_SCREENS && e.Author.Id != Constants.Users.BOT){
                    if (e.Author.Id == Constants.Users.BOT_WINNERS && !content.Contains("updated from battlelog")){ return; }
                    BattleLog bl = BattleLog.Parse(content);
                    if (bl != null){
                        int result = await bl.AddToDatabase();
                        switch (result){
                            case 1:
                                await e.Channel.SendMessageAsync($"Colony n°{bl.PlanetNumber} of {bl.PlayerName} updated.");
                                break;
                            case 2:
                                await e.Channel.SendMessageAsync($"Colony n°{bl.PlanetNumber} of {bl.PlayerName} added.");
                                break;
                            case 3:
                                await e.Channel.SendMessageAsync($"**{bl.PlayerName}** doesn't exist as a player.");
                                break;
                            case 4:
                                await e.Channel.SendMessageAsync($"Colony not found.");
                                break;
                            case 5:
                                await e.Channel.SendMessageAsync($"The coordinates are usually not negative values.");
                                break;
                            case 6:
                                await e.Channel.SendMessageAsync($"The main base coordinates will not be stored.");
                                break;
                            default:
                                await e.Channel.SendMessageAsync($"Colony didn't update. ```{bl}```");
                                break;
                        }
                    }
                    else{
                        await e.Channel.SendMessageAsync($"Please use a **slash command** or the **format of the battlelog** here:\n`<player name> (<planet>: <x>, <y>)`");
                    }
                }
                else if (e.Channel.Id == Constants.Channels.ATTACK_LOGS && e.Author.Id == Constants.Users.BOT){
                    var rh = new RegenHandler(e.Guild, e.Message.Content);
                    await rh.Handle();
                }
                if (content.StartsWith("/")){
                    if (content.StartsWith("/colo_remove")){string pattern = @"(?:colo_number:(1[1-9]|0?[1-9])(?=(?:.*username:(\w+))|.*$)|username:(\w+))";
                        var matches = Regex.Matches(content, pattern, RegexOptions.Multiline);
                        bool allSuccess = true;
                        string username = null;
                        string coloNumber = null;
                        if (matches.Count == 2){
                            bool currentMatchIsUserName = false;
                            for (var j = 0; j < matches.Count; j++){
                                var match = matches[j];
                                for (var i = 0; i < match.Groups.Count; i++){
                                    var group = match.Groups[i];
                                    if (group.Success){
                                        if (group.Value.Length > 0){
                                            if (i == 0){
                                                currentMatchIsUserName = group.Value.ToLower().Contains("username");
                                            }
                                            else if (!group.Value.Contains(':')){
                                                if (currentMatchIsUserName){
                                                    username = group.Value;
                                                }
                                                else{
                                                    coloNumber = group.Value;
                                                }
                                            }
                                        }
                                    }
                                    else if (i == 0) {
                                        allSuccess = false;
                                    }
                                }
                            }
                        }
                        if (allSuccess && username != null && coloNumber != null){
                            var rh = new RegenHandler(e.Guild, e.Message.Content);
                            await rh.Handle(new Tuple<string, string>(username, coloNumber));
                        }
                    }
                    return;
                }
            });
            return Task.CompletedTask;
        }
        private Task DiscordClient_GuildDownloadCompleted(DiscordClient sender, GuildDownloadCompletedEventArgs e)
        {
            _discordClient.Logger.Log(LogLevel.Information, "Client( v" + Version + " ) is ready to process events.");
            _guildDownloadCompleted = true;
            _discordClient.GetGuildAsync(Constants.Guilds.TRUEBLOODALLY3).Result.GetMemberAsync(Constants.Users.THIBEASTMO).Result.SendMessageAsync("Booted up " + Assembly.GetEntryAssembly()?.GetName().Name + " from " + Environment.MachineName);
            return Task.CompletedTask;
        }
        public static Task WriteInfoLog(string text)
        {
            _discordClient.Logger.Log(LogLevel.Information, text);
            return Task.CompletedTask;
        }

        private async Task DiscordClientOnMessageReactionRemoved(DiscordClient sender, MessageReactionRemoveEventArgs e)
        {
            if (e.User.Id != Constants.Users.BOT){
                var rh = new RoleHandler(sender, e.Message, e.Emoji, e.User, false);
                await rh.Handle();
            }
        }
        private async Task DiscordClientOnMessageReactionAdded(DiscordClient sender, MessageReactionAddEventArgs e)
        {
            if (e.User.Id != Constants.Users.BOT){
                var rh = new RoleHandler(sender, e.Message, e.Emoji, e.User, true);
                await rh.Handle();
            }
        }

        #endregion
        private async Task<string> GetMessageContent(DiscordChannel discordChannel, ulong messageId)
        {
            var message = await discordChannel.GetMessageAsync(messageId);
            return message.Content;
        }

        private void StartBackgroundTask()
        {
            while (_backgroundWorker == null){
                Thread.Sleep(100);
            }
            if (!_backgroundWorker.IsBusy){
                _backgroundWorker.RunWorkerAsync();
            }
        }
        public static bool IsWinter(DateTime aDatetime)
        {
            DateTime dateTime1 = new DateTime(aDatetime.Year, 10, 1);
            List<DateTime> dateTimeList1 = new List<DateTime>();
            for (int index = 0; index < 31; ++index){
                if (dateTime1.DayOfWeek == DayOfWeek.Sunday)
                    dateTimeList1.Add(dateTime1);
                dateTime1 = dateTime1.AddDays(1.0);
            }
            dateTime1 = dateTimeList1[dateTimeList1.Count - 1];
            DateTime dateTime2 = new DateTime(aDatetime.Year, 3, 1);
            List<DateTime> dateTimeList2 = new List<DateTime>();
            for (int index = 0; index < 31; ++index){
                if (dateTime2.DayOfWeek == DayOfWeek.Sunday)
                    dateTimeList2.Add(dateTime2);
                dateTime2 = dateTime2.AddDays(1.0);
            }
            dateTime2 = dateTimeList2[dateTimeList2.Count - 1];
            return aDatetime >= dateTime1 || dateTime2 < aDatetime;
        }
    }
}
