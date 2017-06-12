using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SteamKit2;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;

namespace BotSteam
{
    class Program
    {
        static string user, pass;

        static SteamClient steamClient;
        static CallbackManager manager;
        static SteamUser steamUser;
        static SteamFriends steamFriends;
        static string authcode;
        static string twofactor;

        static RootObject config;

        static bool isRunning = false;

        static string authCode;

        static void Main(string[] args)
        {
            if (!File.Exists("chat.txt"))
            {
                File.Create("chat.txt").Close();
                File.WriteAllText("chat.txt", "abc | 123");
            }

            reloadConfig();

            Console.Title = "A bot";
            Console.WriteLine("CTRL+C quits the program.");

            Console.Write("Username: ");
            user = Console.ReadLine();

            Console.Write("Password: ");
            pass = Console.ReadLine();

            SteamLogIn();
        }

        static void SteamLogIn()
        {
            steamClient = new SteamClient();

            manager = new CallbackManager(steamClient);

            steamUser = steamClient.GetHandler<SteamUser>();

            steamFriends = steamClient.GetHandler<SteamFriends>();

            new Callback<SteamClient.ConnectedCallback>(OnConnected, manager);
            new Callback<SteamClient.DisconnectedCallback>(OnDisconnected, manager);

            new Callback<SteamUser.LoggedOnCallback>(OnLoggedOn, manager);
            new Callback<SteamUser.LoggedOffCallback>(OnLoggedOff, manager);

            new Callback<SteamUser.AccountInfoCallback>(OnAccountInfo, manager);
            new Callback<SteamFriends.FriendMsgCallback>(OnChatMessage, manager);


            new Callback<SteamUser.UpdateMachineAuthCallback>(UpdateMachineAuthCallback, manager);

            new Callback<SteamFriends.ChatInviteCallback>(OnChatInvite, manager);
            new Callback<SteamFriends.ChatEnterCallback>(OnChatEnter, manager);
            new Callback<SteamFriends.ChatMsgCallback>(OnGroupMessage, manager);

            isRunning = true;

            Console.WriteLine("Connecting to Steam...");

            steamClient.Connect();


            while (isRunning)
            {
                manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
            }
            Console.ReadKey();
        }

        static void OnConnected(SteamClient.ConnectedCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
                Console.WriteLine("Unable to connect with steam: {0}", callback.Result);
                isRunning = false;
                return;
            }
            Console.WriteLine("Connected to Steam  netork\nLoggin in {0}.......", user);

            byte[] sentryHash = null;

            if (File.Exists("sentry.bin"))
            {
                byte[] sentryFile = File.ReadAllBytes("sentry.bin");

                sentryHash = CryptoHelper.SHAHash(sentryFile);
            }

            steamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = user,
                Password = pass,
                AuthCode = authcode,
                TwoFactorCode = twofactor,
                SentryFileHash = sentryHash,
            });
        }
   static void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            if (callback.Result == EResult.AccountLogonDeniedNeedTwoFactorCode)
            {
                Console.WriteLine("Please Enter In Your Two Factor Auth Code.\n");
                twofactor = Console.ReadLine();
                return;
            }
            if (callback.Result == EResult.AccountLogonDenied)
            {
                Console.WriteLine("Account is steam guard Protected.");

                Console.Write("Please Enter In The Auth Code Sent To The Email At {0}: ", callback.EmailDomain);

                authcode = Console.ReadLine();

                return;
            }
            if (callback.Result != EResult.OK)
            {
                Console.WriteLine("Unable to connect to Steam account: {0}", callback.Result);
                isRunning = false;
                return;
            }
            Console.WriteLine("Succesfully logged in: {0}", callback.Result);
        }
        static void UpdateMachineAuthCallback(SteamUser.UpdateMachineAuthCallback callback)
        {
            Console.WriteLine("Updating Sentry File...");

            byte[] sentryHash = CryptoHelper.SHAHash(callback.Data);

            File.WriteAllBytes("sentry.bin", callback.Data);

            steamUser.SendMachineAuthResponse(new SteamUser.MachineAuthDetails
            {
                JobID = callback.JobID,
                FileName = callback.FileName,
                BytesWritten = callback.BytesToWrite,
                FileSize = callback.Data.Length,
                Offset = callback.Offset,
                Result = EResult.OK,
                LastError = 0,
                OneTimePassword = callback.OneTimePassword,
                SentryFileHash = sentryHash,
            });
            Console.WriteLine("Done.");
        }
        static void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            Console.WriteLine("\n{0} Disconnected From Steam, Reconnecting In 5 Seconds...\n", user);
            Thread.Sleep(TimeSpan.FromSeconds(5));

            steamClient.Connect();
        }
    
        static void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            Console.WriteLine("Logged off of Steam: {0}", callback.Result);
        }

        static void OnAccountInfo(SteamUser.AccountInfoCallback callback)
        {
            steamFriends.SetPersonaState(EPersonaState.Online);

        }

        static void OnChatMessage(SteamFriends.FriendMsgCallback callback)
        {
            string[] args;

            if (callback.EntryType == EChatEntryType.ChatMsg)
            {
                if (callback.Message.Length > 1)
                {
                    //"!"
                    if (callback.Message.Remove(1) == "!")
                    {
                        string command = callback.Message;
                        if (callback.Message.Contains(" ")) //!help
                        {
                            command = callback.Message.Remove(callback.Message.IndexOf(' '));
                        }

                        switch (command)
                        {
                            #region send
                            case "!send": //!send friendname message
                                if (!isBotAdmin(callback.Sender))
                                    break;
                                args = seperate(2, ' ', callback.Message); //args[0] = !send, args[1] = friendname, args[2] = message, args[3-4] = null;
                                Console.WriteLine("!send " + args[1] + args[2] + " command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender));
                                if (args[0] == "-1")
                                {
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Command syntax: !send [friend] [message]");
                                    return;
                                }
                                for (int i = 0; i < steamFriends.GetFriendCount(); i++)
                                {
                                    SteamID friend = steamFriends.GetFriendByIndex(i);
                                    if (steamFriends.GetFriendPersonaName(friend).ToLower().Contains(args[1].ToLower())) //bob dylan !send bob message or !send dylan message
                                    {
                                        steamFriends.SendChatMessage(friend, EChatEntryType.ChatMsg, args[2]);
                                    }
                                }
                                break;
                            #endregion
                            #region friends
                            case "!friends":
                                Console.WriteLine("!friends command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender));
                                for (int i = 0; i < steamFriends.GetFriendCount(); i++)
                                {
                                    SteamID friend = steamFriends.GetFriendByIndex(i);
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Friend: " + steamFriends.GetFriendPersonaName(friend) + "  State: " + steamFriends.GetFriendPersonaState(friend));
                                }
                                break;
                            #endregion
                            #region friend
                            case "!friend":
                                 args = seperate(1, ' ', callback.Message);
                                Console.WriteLine("!friend " + args[1] + " | " + steamFriends.GetFriendPersonaName(Convert.ToUInt64(args[1])) + " command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender));
                                
                                if (!isBotAdmin(callback.Sender))
                                    return;

                                if (args[0] == "-1")
                                {
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Command syntax: !friend [SteamID64]");
                                    return;
                                }
                                try
                                {
                                    SteamID validSID = Convert.ToUInt64(args[1]);
                                    if (!validSID.IsValid)
                                    {
                                        steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Invalid SteamID"); //no person exists with that SID
                                        break;
                                    }
                                    steamFriends.AddFriend(validSID.ConvertToUInt64());
                                }
                                catch (FormatException)
                                {
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Invalid SteamID64.");
                                }
                                break;

                            #endregion
                            #region changename
                            case "!changename":
                                #region theusualstuff
                                if (!isBotAdmin(callback.Sender))
                                    return;
                                args = seperate(1, ' ', callback.Message);
                                Console.WriteLine("!changename " + args[1] + " command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender));
                                if (args[0] == "-1")
                                {
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Syntax: !changename [name]");
                                    return;
                                }
                                #endregion
                                steamFriends.SetPersonaName(args[1]);
                                break;
                            #endregion
                            #region reloadconfig
                            case "!reloadconfig":
                                if (isBotAdmin(callback.Sender))
                                    return;
                                reloadConfig();
                                break;
                            #endregion
                            default :
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Unknown Comand!");
                                break;
                        }
                        return;
                    }
                }
                if (!config.Chatty)
                    return;
                string rLine;
                string trimmed = callback.Message;
                char[] trim = { '!', '@', '#', '$', '%', '^', '&', '*', '(', ')', '-', '_', '=', '+', '[', ']', '{', '}', '\\', '|', ';', ':', '"', '\'', ',', '<', '.', '>', '/', '?' };

                for (int i = 0; i < 30; i++)
                {
                    trimmed = trimmed.Replace(trim[i].ToString(), "");
                }

                StreamReader sReader = new StreamReader("chat.txt");

                while ((rLine = sReader.ReadLine()) != null)
                {
                    string text = rLine.Remove(rLine.IndexOf('|') - 1);
                    string response = rLine.Remove(0, rLine.IndexOf('|') + 2);

                    if (callback.Message.ToLower().Contains(text.ToLower()))
                    {
                        steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, response);
                        sReader.Close();
                        return;
                    }
                }

            }
        }


        static void OnChatInvite(SteamFriends.ChatInviteCallback callback)
        {
            steamFriends.JoinChat(callback.ChatRoomID);
            Console.WriteLine(user + " has been invited to " + callback.ChatRoomName + "'s group chat. (" + callback.ChatRoomID + ")");
        }

        static void OnChatEnter(SteamFriends.ChatEnterCallback callback)
        {
            steamFriends.SendChatRoomMessage(callback.ChatID, EChatEntryType.ChatMsg, "Hi! I am a bot.");
        }

        static void OnGroupMessage(SteamFriends.ChatMsgCallback callback)
        {
            string[] args;

            if (callback.ChatMsgType == EChatEntryType.ChatMsg)
            {
                if (callback.Message.Length > 1)
                {
                    //"!"
                    if (callback.Message.Remove(1) == "!")
                    {
                        string command = callback.Message;
                        if (callback.Message.Contains(" ")) //!help
                        {
                            command = callback.Message.Remove(callback.Message.IndexOf(' '));
                        }

                        switch (command)
                        {
                            #region send
                            case "!send": //!send friendname message
                                if (!isBotAdmin(callback.ChatterID))
                                    break;
                                args = seperate(2, ' ', callback.Message); //args[0] = !send, args[1] = friendname, args[2] = message, args[3-4] = null;
                                Console.WriteLine("!send " + args[1] + args[2] + " command recieved. User: " + steamFriends.GetFriendPersonaName(callback.ChatterID));
                                if (args[0] == "-1")
                                {
                                    steamFriends.SendChatMessage(callback.ChatterID, EChatEntryType.ChatMsg, "Command syntax: !send [friend] [message]");
                                    return;
                                }
                                for (int i = 0; i < steamFriends.GetFriendCount(); i++)
                                {
                                    SteamID friend = steamFriends.GetFriendByIndex(i);
                                    if (steamFriends.GetFriendPersonaName(friend).ToLower().Contains(args[1].ToLower())) //bob dylan !send bob message or !send dylan message
                                    {
                                        steamFriends.SendChatMessage(friend, EChatEntryType.ChatMsg, args[2]);
                                    }
                                }
                                break;
                            #endregion
                            #region friends
                            case "!friends":
                                Console.WriteLine("!friends command recieved. User: " + steamFriends.GetFriendPersonaName(callback.ChatterID));
                                for (int i = 0; i < steamFriends.GetFriendCount(); i++)
                                {
                                    SteamID friend = steamFriends.GetFriendByIndex(i);
                                    steamFriends.SendChatMessage(callback.ChatterID, EChatEntryType.ChatMsg, "Friend: " + steamFriends.GetFriendPersonaName(friend) + "  State: " + steamFriends.GetFriendPersonaState(friend));
                                }
                                break;
                            #endregion
                            #region friend
                            case "!friend":
                                args = seperate(1, ' ', callback.Message);
                                Console.WriteLine("!friend " + args[1] + " | " + steamFriends.GetFriendPersonaName(Convert.ToUInt64(args[1])) + " command recieved. User: " + steamFriends.GetFriendPersonaName(callback.ChatterID));

                                if (!isBotAdmin(callback.ChatterID))
                                    return;

                                if (args[0] == "-1")
                                {
                                    steamFriends.SendChatMessage(callback.ChatterID, EChatEntryType.ChatMsg, "Command syntax: !friend [SteamID64]");
                                    return;
                                }
                                try
                                {
                                    SteamID validSID = Convert.ToUInt64(args[1]);
                                    if (!validSID.IsValid)
                                    {
                                        steamFriends.SendChatMessage(callback.ChatterID, EChatEntryType.ChatMsg, "Invalid SteamID"); //no person exists with that SID
                                        break;
                                    }
                                    steamFriends.AddFriend(validSID.ConvertToUInt64());
                                }
                                catch (FormatException)
                                {
                                    steamFriends.SendChatMessage(callback.ChatterID, EChatEntryType.ChatMsg, "Invalid SteamID64.");
                                }
                                break;

                            #endregion
                            #region changename
                            case "!changename":
                                #region theusualstuff
                                if (!isBotAdmin(callback.ChatterID))
                                    return;
                                args = seperate(1, ' ', callback.Message);
                                Console.WriteLine("!changename " + args[1] + " command recieved. User: " + steamFriends.GetFriendPersonaName(callback.ChatterID));
                                if (args[0] == "-1")
                                {
                                    steamFriends.SendChatMessage(callback.ChatterID, EChatEntryType.ChatMsg, "Syntax: !changename [name]");
                                    return;
                                }
                                #endregion
                                steamFriends.SetPersonaName(args[1]);
                                break;
                            #endregion
                            #region reloadconfig
                            case "!reloadconfig":
                                if (isBotAdmin(callback.ChatterID))
                                    return;
                                reloadConfig();
                                break;
                            #endregion

                            default:
                                steamFriends.SendChatMessage(callback.ChatterID, EChatEntryType.ChatMsg, "Unknown Comand!");
                                break;

                        }
                        return;
                    }
                }
                if (!config.Chatty)
                    return;
                string rLine;
                string trimmed = callback.Message;
                char[] trim = { '!', '@', '#', '$', '%', '^', '&', '*', '(', ')', '-', '_', '=', '+', '[', ']', '{', '}', '\\', '|', ';', ':', '"', '\'', ',', '<', '.', '>', '/', '?' };

                for (int i = 0; i < 30; i++)
                {
                    trimmed = trimmed.Replace(trim[i].ToString(), "");
                }

                StreamReader sReader = new StreamReader("chat.txt");

                while ((rLine = sReader.ReadLine()) != null)
                {
                    string text = rLine.Remove(rLine.IndexOf('|') - 1);
                    string response = rLine.Remove(0, rLine.IndexOf('|') + 2);

                    if (callback.Message.ToLower().Contains(text.ToLower()))
                    {
                        steamFriends.SendChatRoomMessage(callback.ChatRoomID, EChatEntryType.ChatMsg, response);
                        sReader.Close();
                        return;
                    }
                }

            }
        }
        public static bool isBotAdmin(SteamID sid)
        {
            try
            {
                foreach(UInt16 ui in config.Admins)
                    if (ui == sid)
                        return true;

                steamFriends.SendChatMessage(sid, EChatEntryType.ChatMsg, "You are not a bot admin.");
                Console.WriteLine(steamFriends.GetFriendPersonaName(sid) + "attempted to use an adminstrator comand wile not an administrator.");
                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }
        
        public static string[] seperate(int number, char seperator, string thestring)
        {
            string[] returned = new string[4];

            int i = 0;

            int error = 0;

            int length = thestring.Length;

            foreach (char c in thestring) //!friend
            {
                if (i != number)
                {
                    if (error > length || number > 5)
                    {
                        returned[0] = "-1";
                        return returned;
                    }
                    else if (c == seperator)
                    {
                        //returned[0] = !friend
                        returned[i] = thestring.Remove(thestring.IndexOf(c));
                        thestring = thestring.Remove(0, thestring.IndexOf(c) + 1);
                        i++;
                    }
                    error++;

                    if (error == length && i != number)
                    {
                        returned[0] = "-1";
                        return returned;
                    }
                }
                else
                {
                    returned[i] = thestring;
                }
            }
            return returned;
        }

        public static void reloadConfig()
        {
            if (!File.Exists("config.cfg"))
            {
                StringBuilder sb = new StringBuilder();

                sb.Append("{\r\n");
                sb.Append("\"Admins\":[76561198288257439],\r\n");
                sb.Append("\"Chatty\":false\r\n");
                sb.Append("}\r\n");

                File.WriteAllText("config.cfg",sb.ToString());
            }

            try
            {
                JavaScriptSerializer jss = new JavaScriptSerializer();
                config = jss.Deserialize<RootObject>(File.ReadAllText("config.cfg"));

            }
            catch(Exception e){
                Console.WriteLine("Oh no an error" + e.Message);
                Console.ReadKey();
                reloadConfig();
            }
        }
    
    }

    public class RootObject
    {
        public List<UInt64> Admins { get; set; }
        public bool Chatty { get; set; }
    }
}