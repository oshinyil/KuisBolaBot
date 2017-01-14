using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;

namespace KuisBolaBot.WebJob
{
    public class Program
    {
        private static bool stopMe = false;
        private static List<int> updateIds = new List<int>();

        public static void Main(string[] args)
        {
            Console.WriteLine("Starting the bot...");
            Console.WriteLine();

            var t = Task.Run(() => RunBot(Configuration.Instance["BotAccessToken"]));

            Console.ReadLine();
            stopMe = true;
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
                            var response = GameManager.Start(update.Message.Chat.Id);
                            string replyMessage;
                            switch (response)
                            {
                                case "SUCCESS":
                                    replyMessage = "Game has been started successfully.";
                                    break;
                                case "EXISTS":
                                    replyMessage = "Game is already running.";
                                    break;
                                case "FAILED":
                                default:
                                    replyMessage = "Failed to start a new game.";
                                    break;
                            }

                            bot.SendTextMessageAsync(update.Message.Chat.Id, replyMessage).Wait();

                            if (response == "SUCCESS")
                            {
                                var question = GameManager.GenerateQuestion(update.Message.Chat.Id);
                                if (!string.IsNullOrEmpty(question.ImageUrl))
                                {
                                    bot.SendPhotoAsync(update.Message.Chat.Id, question.ImageUrl).Wait();
                                }
                                bot.SendTextMessageAsync(update.Message.Chat.Id, question.Message).Wait();
                            }
                        }

                        if (text == "/next")
                        {
                            if (GameManager.HasGameStarted(update.Message.Chat.Id))
                            {
                                var question = GameManager.GenerateQuestion(update.Message.Chat.Id);
                                if (question == null)
                                {
                                    bot.SendTextMessageAsync(update.Message.Chat.Id, "Game ended.").Wait();
                                }

                                if (!string.IsNullOrEmpty(question.ImageUrl))
                                {
                                    bot.SendPhotoAsync(update.Message.Chat.Id, question.ImageUrl).Wait();
                                }
                                bot.SendTextMessageAsync(update.Message.Chat.Id, question.Message).Wait();
                            }
                            else
                            {
                                bot.SendTextMessageAsync(update.Message.Chat.Id, "No game running. Please use command /start to start new game.").Wait();
                            }
                        }

                        if (text == "/end")
                        {
                            if (GameManager.HasGameStarted(update.Message.Chat.Id))
                            {
                                GameManager.EndGame(update.Message.Chat.Id);
                                bot.SendTextMessageAsync(update.Message.Chat.Id, "Game ended.").Wait();
                            }
                            else
                            {
                                bot.SendTextMessageAsync(update.Message.Chat.Id, "No game running. Please use command /start to start new game.").Wait();
                            }
                        }
                    }
                }
            }
        }
    }
}
