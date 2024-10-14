using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TelegramGiveawayBot
{
    class Program
    {
        // Токен вашего бота
        private const string BotToken = "7055767364:AAHbA2neU5q1BCRyzZ02iz_-XhRGcm2RFfw";
        // Название базы данных
        private const string DatabaseName = "giveaway_database";
        // Название коллекции
        private const string CollectionName = "giveaways";

        // Клиент MongoDB
        private static IMongoClient _client;
        private static IMongoDatabase _database;
        private static IMongoCollection<Giveaway> _giveawayCollection;

        // Клиент Telegram Bot
        private static TelegramBotClient _bot;

        static void Main(string[] args)
        {
            // Инициализация MongoDB
            _client = new MongoClient("mongodb://localhost:27017");
            _database = _client.GetDatabase(DatabaseName);
            _giveawayCollection = _database.GetCollection<Giveaway>(CollectionName);

            // Инициализация Telegram Bot
            _bot = new TelegramBotClient(BotToken);
            _bot.OnMessage += BotOnMessageReceived;

            // Запуск бота
            Console.WriteLine("Bot started. Press Ctrl+C to stop.");
            _bot.StartReceiving();
            Console.ReadLine();
            _bot.StopReceiving();
        }

        // Обработчик сообщений
        private static async Task BotOnMessageReceived(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            var message = e.Message;
            if (message == null || message.Type != MessageType.Text) return;

            // Обработка команд
            switch (message.Text.ToLower())
            {
                case "/start":
                    await _bot.SendTextMessageAsync(message.Chat.Id, "Добро пожаловать! Я бот для розыгрышей.\n" +
                        "Используйте команды: /create, /join, /list, /end.");
                    break;
                case "/create":
                    await _bot.SendTextMessageAsync(message.Chat.Id, "Введите текст розыгрыша:");
                    _bot.OnMessage += HandleGiveawayCreation;
                    break;
                case "/join":
                    await _bot.SendTextMessageAsync(message.Chat.Id, "Введите ID розыгрыша, в который хотите вступить:");
                    _bot.OnMessage += HandleGiveawayJoin;
                    break;
                case "/list":
                    await ListGiveaways(message.Chat.Id);
                    break;
                case "/end":
                    await _bot.SendTextMessageAsync(message.Chat.Id, "Введите ID розыгрыша, который хотите завершить:");
                    _bot.OnMessage += HandleGiveawayEnd;
                    break;
                default:
                    await _bot.SendTextMessageAsync(message.Chat.Id, "Неизвестная команда.");
                    break;
            }
        }

        // Обработка создания розыгрыша
        private static async Task HandleGiveawayCreation(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            var message = e.Message;
            if (message == null || message.Type != MessageType.Text) return;

            // Создание нового розыгрыша
            var giveaway = new Giveaway
            {
                Text = message.Text,
                Participants = new List<long>() { message.From.Id }
            };

            // Вставка розыгрыша в базу данных
            await _giveawayCollection.InsertOneAsync(giveaway);

            // Отправка подтверждения
            await _bot.SendTextMessageAsync(message.Chat.Id, "Розыгрыш создан! ID: " + giveaway.Id);

            _bot.OnMessage -= HandleGiveawayCreation;
        }

        // Обработка вступления в розыгрыш
        private static async Task HandleGiveawayJoin(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            var message = e.Message;
            if (message == null || message.Type != MessageType.Text) return;

            if (!long.TryParse(message.Text, out long giveawayId))
            {
                await _bot.SendTextMessageAsync(message.Chat.Id, "Неверный формат ID розыгрыша.");
                return;
            }

            // Поиск розыгрыша
            var giveaway = await _giveawayCollection.Find(x => x.Id == giveawayId).FirstOrDefaultAsync();
            if (giveaway == null)
            {
                await _bot.SendTextMessageAsync(message.Chat.Id, "Розыгрыш не найден.");
                return;
            }

            // Добавление участника
            if (!giveaway.Participants.Contains(message.From.Id))
            {
                giveaway.Participants.Add(message.From.Id);
                await _giveawayCollection.ReplaceOneAsync(x => x.Id == giveawayId, giveaway);
                await _bot.SendTextMessageAsync(message.Chat.Id, "Вы добавлены в розыгрыш!");
            }
            else
            {
                await _bot.SendTextMessageAsync(message.Chat.Id, "Вы уже участвуете в этом розыгрыше.");
            }

            _bot.OnMessage -= HandleGiveawayJoin;
        }

        // Обработка завершения розыгрыша
        private static async Task HandleGiveawayEnd(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            var message = e.Message;
            if (message == null || message.Type != MessageType.Text) return;

            if (!long.TryParse(message.Text, out long giveawayId))
            {
                await _bot.SendTextMessageAsync(message.Chat.Id, "Неверный формат ID розыгрыша.");
                return;
            }

            // Поиск розыгрыша
            var giveaway = await _giveawayCollection.Find(x => x.Id == giveawayId).FirstOrDefaultAsync();
            if (giveaway == null)
            {
                await _bot.SendTextMessageAsync(message.Chat.Id, "Розыгрыш не найден.");
                return;
            }

            // Выбор победителя
            var winner = giveaway.Participants.ElementAt(
