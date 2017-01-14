using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;

namespace KuisBolaBot.WebJob
{
    public class Program
    {
        private static bool stopMe = false;
        private static bool stopGame = false;
        private static List<int> updateIds = new List<int>();

        public static void Main(string[] args)
        {
            Console.WriteLine("Starting the bot...");
            Console.WriteLine();

            var task = Task.Run(() => RunBot(Configuration.Instance["BotAccessToken"]));

            Console.ReadLine();
            stopMe = true;
            stopGame = true;
        }

        public static void RunBot(string accessToken)
        {
            var bot = new TelegramBotClient(accessToken);

            var me = bot.GetMeAsync().Result;
            if (me == null)
            {
                Console.WriteLine("GetMe() FAILED.");
                Console.WriteLine("(Press ENTER to quit)");
                Console.ReadLine();
                return;
            }
            Console.WriteLine("{0} (@{1}) connected!", me.FirstName, me.Username);

            var gameManager = new GameManager(bot);
            var gameTask = Task.Run(() => RunGame(gameManager));

            Console.WriteLine();
            Console.WriteLine("Find @{0} in Telegram and send him a message - it will be displayed here", me.Username);
            Console.WriteLine("(Press ENTER to stop listening and quit)");
            Console.WriteLine();

            int offset = 0;
            while (!stopMe)
            {
                var updates = bot.GetUpdatesAsync(offset).Result;
                if (updates != null)
                {
                    foreach (var update in updates.Where(u => !updateIds.Contains(u.Id)))
                    {
                        offset = update.Id + 1;
                        if (update.Message == null)
                        {
                            continue;
                        }
                        var from = update.Message.From;
                        var text = update.Message.Text;
                        Console.WriteLine(
                            "Msg from {0} {1} ({2}) at {4}: {3}",
                            from.FirstName,
                            from.LastName,
                            from.Username,
                            text,
                            update.Message.Date);

                        if (string.IsNullOrEmpty(text))
                        {
                            continue;
                        }

                        if (text == "/start")
                        {
                            gameManager.Start(update.Message.Chat.Id, update.Message.From.Username);
                        }
                        else if (text == "/next")
                        {
                            gameManager.SendQuestion(update.Message.Chat.Id);
                        }
                        else if (text == "/join")
                        {
                            gameManager.Join(update.Message.Chat.Id, update.Message.From.Username);
                        }
                        else if (text == "/end")
                        {
                            gameManager.End(update.Message.Chat.Id, update.Message.From.Username);
                        }
                    }
                }
            }
        }

        public static void RunGame(GameManager gameManager)
        {
            while (!stopGame)
            {
                gameManager.RunWorker();
                Thread.Sleep(1000);
            }
        }
    }
}
