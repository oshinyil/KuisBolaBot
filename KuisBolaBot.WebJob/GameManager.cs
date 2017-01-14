using KuisBolaBot.WebJob.Models;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telegram.Bot;

namespace KuisBolaBot.WebJob
{
    public class GameManager
    {
        private static string dbConnection = Configuration.Instance["DatabaseConnection"];
        private static string dbName = Configuration.Instance["DatabaseName"];
        private static List<Game> games = new List<Game>();
        private static MongoClient client = new MongoClient(dbConnection);
        private static IMongoDatabase db = client.GetDatabase(dbName);

        private TelegramBotClient bot;

        #region Public Methods

        public GameManager(TelegramBotClient bot)
        {
            this.bot = bot;
        }

        public void Start(long chatId, string userName)
        {
            if (HasGameStarted(chatId))
            {
                bot.SendTextMessageAsync(chatId, "Permainan sedang berlangsung.").Wait();
                return;
            }

            games.Add(new Game
            {
                Id = new MongoDB.Bson.ObjectId(),
                ChatId = chatId,
                StartDate = DateTime.Now,
                StartedBy = userName
            });
            bot.SendTextMessageAsync(chatId, "Permainan telah berhasil dimulai.").Wait();

            Join(chatId, userName);
            SendQuestion(chatId);
        }

        public void End(long chatId, string userName)
        {
            var game = GetCurrentGame(chatId);

            if (game == null)
            {
                SendNoGameMessage(chatId);
                return;
            }

            if (game.StartedBy != userName)
            {
                bot.SendTextMessageAsync(
                    chatId,
                    string.Format("Maaf, hanya @{0} yang boleh menghentikan permainan.", game.StartedBy)
                ).Wait();
                return;
            }

            EndGame(game);
        }

        public void Join(long chatId, string userName)
        {
            var game = GetCurrentGame(chatId);

            if (game == null)
            {
                SendNoGameMessage(chatId);
                return;
            }

            if (game.Players.Any(p => p.UserName == userName))
            {
                bot.SendTextMessageAsync(
                    chatId,
                    string.Format("@{0} telah bergabung dalam permainan sebelumnya.", userName)
                ).Wait();
                return;
            }

            game.Players.Add(new Player
            {
                UserName = userName,
                JoinedDate = DateTime.Now
            });

            bot.SendTextMessageAsync(
                chatId,
                string.Format("@{0} berhasil bergabung dalam permainan.", userName)
            ).Wait();
        }

        public void SendQuestion(long chatId)
        {
            var game = GetCurrentGame(chatId);

            if (game == null)
            {
                SendNoGameMessage(chatId);
                return;
            }

            if (game.IssuedQuestions.Count == 5)
            {
                EndGame(game);
                return;
            }

            var quiz = GetRandomQuiz(game);
            if (quiz == null)
            {
                EndGame(game);
                bot.SendTextMessageAsync(chatId, "Maaf, tidak ada soal yang tersedia. Permainan dihentikan.").Wait();
                return;
            }

            var issuedQuestion = new IssuedQuestion
            {
                Quiz = quiz,
                CreatedDate = DateTime.Now
            };

            game.CurrentQuizId = issuedQuestion.Quiz.Id;
            game.CurrentQuizStartedDate = issuedQuestion.CreatedDate;
            game.IssuedQuestions.Add(issuedQuestion);

            var sbQuestion = new StringBuilder();
            sbQuestion.AppendLine(quiz.Question);
            if (quiz.Type == QuestionType.MultipleChoice)
            {
                foreach (var choice in quiz.AnswerChoices.OrderBy(a => a.No))
                {
                    sbQuestion.AppendLine(string.Format("{0}. {1}", choice.No, choice.Answer));
                }
            }

            bot.SendTextMessageAsync(chatId, "Soal baru sedang dikirim...").Wait();
            if (!string.IsNullOrEmpty(quiz.ImageUrl))
            {
                bot.SendPhotoAsync(chatId, quiz.ImageUrl).Wait();
            }
            var message = bot.SendTextMessageAsync(chatId, sbQuestion.ToString()).Result;
            issuedQuestion.MessageId = message.MessageId;
        }

        public void RunWorker()
        {
            if (games.Any())
            {
                foreach (var game in games)
                {
                    CheckGameExpiry(game);
                }
            }
        }

        #endregion

        #region Private Methods

        private static bool HasGameStarted(long chatId)
        {
            return games.Any(g => g.ChatId == chatId);
        }

        private void CheckGameExpiry(Game game)
        {
            Console.WriteLine("Game Id = {0}; Current Quiz Id = {1}", game.Id, game.CurrentQuizId);

            if (game.IssuedQuestions.Count == 0)
            {
                return;
            }

            if ((DateTime.Now - game.CurrentQuizStartedDate).TotalMinutes >= 1)
            {
                bot.SendTextMessageAsync(game.ChatId, "Waktu untuk menjawab telah habis. Lanjut ke soal berikutnya.").Wait();
                SendQuestion(game.ChatId);

                return;
            }
        }


        private void ShowWinner(Game game)
        {
            var sbMessage = new StringBuilder();

            var maxPoint = game.Players.Max(p => p.Point);
            if (maxPoint <= 0)
            {
                sbMessage.AppendLine("Tidak ada pemenang dalam permainan ini.");
            }
            else
            {
                var winners = game.Players.Where(p => p.Point == maxPoint).ToList();

                sbMessage.AppendLine(string.Format("Selamat, pemenang permainan ini dengan raihan {0} poin adalah:", maxPoint));
                foreach (var winner in winners)
                {
                    sbMessage.AppendLine(string.Format("@{0}", winner.UserName));
                }
            }

            bot.SendTextMessageAsync(game.ChatId, sbMessage.ToString()).Wait();
        }

        private static Quiz GetRandomQuiz(Game game)
        {
            var collection = db.GetCollection<Quiz>("Quiz");

            var filter = Builders<Quiz>.Filter.Where(q => !game.GetPlayedQuizIds().Contains(q.Id));
            var quizzes = collection.Find(filter).ToList();

            if (quizzes == null || quizzes.Count == 0)
            {
                return null;
            }

            var random = new Random();
            var quiz = quizzes[random.Next(quizzes.Count)];

            return quiz;
        }

        private static Game GetCurrentGame(long chatId)
        {
            return games.Where(g => g.ChatId == chatId).FirstOrDefault();
        }

        private void SendNoGameMessage(long chatId)
        {
            bot.SendTextMessageAsync(
                chatId,
                string.Format("Tidak ada permainan yang sedang berlangsung. Gunakan perintah /start untuk memulai permainan baru.")
            ).Wait();
        }

        private void EndGame(Game game)
        {
            ShowWinner(game);
            game.EndDate = DateTime.Now;
            SaveGame(game);
            games.Remove(game);
            bot.SendTextMessageAsync(game.ChatId, "Permainan telah berakhir.").Wait();
        }

        private static void SaveGame(Game game)
        {
            try
            {
                var collection = db.GetCollection<Game>("Game");
                collection.InsertOne(game);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to save GameId = {0}. Exception = {1}", game.Id, ex.ToString());
            }
        }

        #endregion
    }
}
