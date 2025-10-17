// Program.cs - VERSÃO FINAL HÍBRIDA (ML.NET + Markov + Gemini)
// SPDX-License-Identifier: GPL-3.0 
// Copyright (c) 2025 Victor Pereira
using Discord;
using Discord.WebSocket;
using MeuBotGenerativo;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static MeuBotGenerativo.ModelTrainer;

public class Program
{
    private DiscordSocketClient _client;
    private readonly ChatDbContext _dbContext = new ChatDbContext();

    // --- NOSSOS 3 CÉREBROS ---
    // 1. O Gerente de Intenções
    private readonly PredictionEngine<ModelInput, ModelOutput> _intentPredictionEngine;
    // 2. Os Especialistas de Personalidade
    private readonly Dictionary<string, MarkovChainGenerator> _markovBrains = new Dictionary<string, MarkovChainGenerator>();
    // 3. O Especialista de Conhecimento (e sua conexão)
    private static readonly HttpClient httpClient = new HttpClient();
    private const string GeminiApiKey = ""; // <-- COLE A CHAVE DO GEMINI AQUI

    // O extrator de palavras-chave que já tínhamos
    private readonly KeywordExtractor _keywordExtractor;

    public static Task Main(string[] args) => new Program().MainAsync();

    public Program()
    {
        var mlContext = new MLContext();
        ITransformer trainedModel = mlContext.Model.Load("intent_model.zip", out var modelInputSchema);
        _intentPredictionEngine = mlContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(trainedModel);
        Console.WriteLine("Modelo de intenção do ML.NET carregado.");

        using var db = new ChatDbContext();
        var allMessages = db.Messages.ToList();
        _keywordExtractor = new KeywordExtractor(allMessages);
    }

    public async Task MainAsync()
    {
        var config = new DiscordSocketConfig { GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent };
        _client = new DiscordSocketClient(config);
        _client.Log += Log;
        _client.MessageReceived += MessageReceivedHandler;

        // Treina os cérebros de Markov (como antes)
        var allMessages = await _dbContext.Messages.ToListAsync();
        var messagesByIntent = allMessages.GroupBy(m => PredictIntent(m.Content));
        foreach (var group in messagesByIntent)
        {
            var intent = group.Key;
            var messagesForIntent = group.ToList();
            _markovBrains[intent] = new MarkovChainGenerator();
            _markovBrains[intent].Train(messagesForIntent);
            Console.WriteLine($"Cérebro de Markov treinado para a intenção '{intent}' com {messagesForIntent.Count} mensagens.");
        }

        var token = ""; // <-- COLE SEU TOKEN AQUI DISCORD

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();
        await Task.Delay(-1);
    }

    private string PredictIntent(string text)
    {
        var input = new ModelInput { Frase = text };
        var prediction = _intentPredictionEngine.Predict(input);
        return prediction.PredictedIntencao ?? "afirmacao";
    }

    private Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }

    private async Task MessageReceivedHandler(SocketMessage message)
    {
        if (message.Author.IsBot) return;

        // Mantém todos os logs e lógicas de salvamento e aprendizado contínuo que você já tinha
        Console.WriteLine($"\n--- Nova Mensagem Recebida de '{message.Author.Username}' ---");
        Console.WriteLine($"Conteúdo: \"{message.Content}\"");
        var receivedMessageEntity = new ChatMessage
        {
            Id = message.Id,
            AuthorId = message.Author.Id,
            AuthorUsername = message.Author.Username,
            Content = message.Content,
            Timestamp = message.Timestamp.UtcDateTime
        };
        await _dbContext.Messages.AddAsync(receivedMessageEntity);
        await _dbContext.SaveChangesAsync();
        Console.WriteLine("[LOG] Mensagem salva no banco de dados.");
        var intentDaMensagem = PredictIntent(receivedMessageEntity.Content);
        if (!_markovBrains.ContainsKey(intentDaMensagem)) { _markovBrains[intentDaMensagem] = new MarkovChainGenerator(); }
        _markovBrains[intentDaMensagem].Train(new List<ChatMessage> { receivedMessageEntity });
        Console.WriteLine($"[LOG] Cérebro '{intentDaMensagem}' foi atualizado com a nova mensagem.");

        // --- LÓGICA DO ORQUESTRADOR ---
        if (message.MentionedUsers.Any(user => user.Id == _client.CurrentUser.Id))
        {
            _ = Task.Run(async () =>
            {
                await message.Channel.TriggerTypingAsync();

                // 1. O GERENTE (ML.NET) ANALISA A INTENÇÃO
                var userIntent = PredictIntent(message.Content);
                Console.WriteLine($"[LOG] Intenção detectada: '{userIntent}'. O gerente está decidindo...");

                string botResponseText = null;

                // 2. O GERENTE DELEGA A TAREFA
                switch (userIntent)
                {
                    case "pergunta":
                        Console.WriteLine("[LOG] Tarefa delegada ao Especialista de Conhecimento (Gemini).");
                        var history = await _dbContext.Messages.OrderByDescending(m => m.Timestamp).Take(20).ToListAsync();
                        history.Reverse();
                        botResponseText = await GenerateGeminiResponseAsync(history);
                        break;

                    case "saudacao":
                    case "despedida":
                    case "agradecimento":
                    case "afirmacao":
                    default: // Se for qualquer outra intenção conhecida ou desconhecida, usa o Markov
                        Console.WriteLine($"[LOG] Tarefa delegada ao Especialista de Personalidade (Markov - cérebro '{userIntent}').");
                        if (_markovBrains.TryGetValue(userIntent, out var relevantBrain))
                        {
                            // Usa a lógica de palavras-chave que já tínhamos
                            var keywords = _keywordExtractor.ExtractKeywords(message.Content);
                            foreach (var keyword in keywords)
                            {
                                var response = relevantBrain.GenerateResponse(keyword, 25);
                                if (!response.StartsWith("Não sei o que dizer"))
                                {
                                    botResponseText = response;
                                    break;
                                }
                            }
                            if (botResponseText == null)
                            {
                                botResponseText = relevantBrain.GenerateRandomResponse(25);
                            }
                        }
                        break;
                }

                botResponseText ??= "Fiquei um pouco confuso, pode tentar de novo?";

                Console.WriteLine($"[LOG] Resposta final gerada: \"{botResponseText}\"");
                await message.Channel.SendMessageAsync(botResponseText);

                // Salva a resposta do bot (seja do Gemini ou Markov) no histórico
                var botMessageEntity = new ChatMessage
                {
                    Id = 0,
                    AuthorId = _client.CurrentUser.Id,
                    AuthorUsername = _client.CurrentUser.Username,
                    Content = botResponseText,
                    Timestamp = DateTime.UtcNow
                };
                await _dbContext.Messages.AddAsync(botMessageEntity);
                await _dbContext.SaveChangesAsync();
            });
        }
        Console.WriteLine("--- Processo da Mensagem Concluído ---");
    }

    // Método auxiliar para chamar a API do Gemini (nosso "Plano B")
    private async Task<string> GenerateGeminiResponseAsync(List<ChatMessage> history)
    {
        try
        {
            var requestBody = new
            {
                contents = history.Select(msg => new
                {
                    role = msg.AuthorId == _client.CurrentUser.Id ? "model" : "user",
                    parts = new[] { new { text = msg.Content } }
                })
            };

            var jsonContent = JsonConvert.SerializeObject(requestBody);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-latest:generateContent?key={GeminiApiKey}";
            var response = await httpClient.PostAsync(apiUrl, httpContent);

            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                var parsedJson = JObject.Parse(responseString);
                return parsedJson["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString() ?? "Gemini não retornou uma resposta válida.";
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Erro da API Gemini: {response.StatusCode} - {errorContent}");
                return "Desculpe, meu cérebro de conhecimento (Gemini) está com um problema no momento.";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro inesperado na chamada do Gemini: {ex.Message}");
            return "Ocorreu um erro crítico ao tentar contatar o Gemini.";
        }
    }
}