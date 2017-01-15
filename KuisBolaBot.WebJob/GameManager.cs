using KuisBolaBot.WebJob.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

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
                bot.SendTextMessageAsync(chatId, "Permainan KuisBolaBot sedang berlangsung.").Wait();
                return;
            }

            games.Add(new Game
            {
                Id = new ObjectId(),
                ChatId = chatId,
                StartDate = DateTime.Now,
                StartedBy = userName
            });
            bot.SendTextMessageAsync(chatId, "Permainan KuisBolaBot telah berhasil dimulai.").Wait();

            Join(chatId, userName);

            Thread.Sleep(1000);
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
            sbQuestion.AppendLine();
            sbQuestion.AppendLine(string.Format("Soal bernilai {0}", quiz.Type == QuestionType.MultipleChoice ? 1 : 3));
            sbQuestion.AppendLine("*Reply soal ini untuk menjawab*");

            bot.SendTextMessageAsync(chatId, "Soal baru sedang dikirim...").Wait();
            if (!string.IsNullOrEmpty(quiz.ImageUrl))
            {
                bot.SendPhotoAsync(chatId, quiz.ImageUrl).Wait();
            }
            var message = bot.SendTextMessageAsync(chatId, sbQuestion.ToString()).Result;
            issuedQuestion.MessageId = message.MessageId;
        }

        public void Answer(Update update)
        {
            if (update.Message.ReplyToMessage == null)
            {
                return;
            }

            var game = GetCurrentGame(update.Message.Chat.Id);
            if (game == null)
            {
                return;
            }

            var answeredQuestion = game.IssuedQuestions
                .Where(q => q.MessageId == update.Message.ReplyToMessage.MessageId && q.Winner == null)
                .FirstOrDefault();
            if (answeredQuestion == null)
            {
                return;
            }

            var player = game.Players.Where(p => p.UserName == update.Message.From.Username).FirstOrDefault();
            if (player == null)
            {
                bot.SendTextMessageAsync(
                    update.Message.Chat.Id, 
                    string.Format(
                        "Maaf @{0}, Anda belum bergabung dalam permainan ini. Gunakan perintah /join untuk bergabung.", 
                        update.Message.From.Username));

                return;
            }

            if (string.Equals(answeredQuestion.Quiz.Answer, update.Message.Text, StringComparison.OrdinalIgnoreCase))
            {
                answeredQuestion.Winner = player.UserName;
                answeredQuestion.AnsweredDate = DateTime.Now;
                player.Points += answeredQuestion.Quiz.Type == QuestionType.MultipleChoice ? 1 : 3;

                bot.SendTextMessageAsync(
                    update.Message.Chat.Id,
                    string.Format(
                        "Selamat @{0}, Anda berhasil menjawab dengan tepat.",
                        update.Message.From.Username));

                Thread.Sleep(1000);
                SendQuestion(update.Message.Chat.Id);
            }
        }

        public async void ShowTable(long chatId, string username)
        {
            try
            {
                var sbMessage = new StringBuilder();
                sbMessage.AppendLine("Klasmen sementara KuisBolaBot:");

                var collection = db.GetCollection<Table>("Table");
                var filter = Builders<Table>.Filter.Empty;
                var tables = await collection.Find(filter).SortByDescending(t => t.Points).Limit(3).ToListAsync();

                for (int i = 0; i < tables.Count; i++)
                {
                    sbMessage.AppendLine(
                        string.Format("{0}. {1} (Main:{2} - Poin:{3})",
                            i + 1,
                            tables[i].UserName,
                            tables[i].GamesPlayed,
                            tables[i].Points
                        )
                    );
                }

                if (!tables.Any(t => t.UserName == username))
                {
                    filter = Builders<Table>.Filter.Eq("UserName", username);
                    var table = await collection.Find(filter).FirstOrDefaultAsync();

                    filter = Builders<Table>.Filter.Gt("Points", table.Points);
                    long position = 1;
                    position += await collection.Find(filter).CountAsync();

                    if ((position - tables.Count) > 1)
                    {
                        sbMessage.AppendLine("...");
                    }

                    sbMessage.AppendLine(
                        string.Format("{0}. {1} (Main:{2} - Poin:{3})",
                            position,
                            table.UserName,
                            table.GamesPlayed,
                            table.Points
                        )
                    );
                }

                await bot.SendTextMessageAsync(chatId, sbMessage.ToString());

            }
            catch (Exception ex)
            {
                await bot.SendTextMessageAsync(chatId, "Maaf, tidak dapat menampilkan klasmen sementara.");
                Console.WriteLine("Failed to display table. Exception = {0}", ex.ToString());
            }
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

            if ((DateTime.Now - game.CurrentQuizStartedDate).TotalSeconds >= 30
                && game.IssuedQuestions.Any(q => q.Quiz.Id == game.CurrentQuizId && q.Winner == null ))
            {
                bot.SendTextMessageAsync(game.ChatId, "Waktu untuk menjawab telah habis. Lanjut ke soal berikutnya.").Wait();
                SendQuestion(game.ChatId);

                return;
            }
        }


        private void ShowWinner(Game game)
        {
            var sbMessage = new StringBuilder();

            var maxPoint = game.Players.Max(p => p.Points);
            if (maxPoint <= 0)
            {
                sbMessage.AppendLine("Tidak ada pemenang dalam permainan ini.");
            }
            else
            {
                var winners = game.Players.Where(p => p.Points == maxPoint).ToList();

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
            try
            {
                var collection = db.GetCollection<Quiz>("Quiz");

                var filter = Builders<Quiz>.Filter.Where(q => !game.GetPlayedQuizIds().Contains(q.Id));
                var quizzes = collection.FindAsync(filter).Result.ToList();

                if (quizzes == null || quizzes.Count == 0)
                {
                    return null;
                }

                var random = new Random();
                var quiz = quizzes[random.Next(quizzes.Count)];

                return quiz;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to get random quiz. Exception = {0}", ex.ToString());
                return null;
            }
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

            Task.Run(() => UpdateTable(game.Players));

            games.Remove(game);

            bot.SendTextMessageAsync(game.ChatId, "Permainan telah berakhir.").Wait();
        }

        private static void SaveGame(Game game)
        {
            try
            {
                var collection = db.GetCollection<Game>("Game");
                collection.InsertOneAsync(game);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to save GameId = {0}. Exception = {1}", game.Id, ex.ToString());
            }
        }

        private async void UpdateTable(IEnumerable<Player> players)
        {
            var collection = db.GetCollection<Table>("Table");

            foreach (var player in players)
            {
                try
                {
                    var filter = Builders<Table>.Filter.Eq("UserName", player.UserName);
                    var table = await collection.Find(filter).FirstOrDefaultAsync();

                    if (table == null)
                    {
                        table = new Table
                        {
                            UserName = player.UserName,
                            CreatedDate = DateTime.Now
                        };
                    }

                    table.GamesPlayed += 1;
                    table.Points += player.Points;
                    table.UpdatedDate = DateTime.Now;

                    await collection.ReplaceOneAsync(
                        t => t.Id == table.Id,
                        table,
                        new UpdateOptions { IsUpsert = true });
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to update table. Username = {0}. Exception = {1}", player.UserName, ex.ToString());
                }
            }
        }

        #endregion
    }
}
