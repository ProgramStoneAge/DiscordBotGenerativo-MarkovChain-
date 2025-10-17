// MarkovChainGenerator.cs - VERSÃO 2.0 COM MEMÓRIA DUPLA
// SPDX-License-Identifier: GPL-3.0 
// Copyright (c) 2025 Victor Pereira

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions; // Adicionado para limpeza avançada

namespace MeuBotGenerativo
{
    internal class MarkovChainGenerator
    {
        // O dicionário agora usa uma string contendo DUAS palavras como chave.
        private readonly Dictionary<string, List<string>> _chain = new Dictionary<string, List<string>>();
        private readonly Random _random = new Random();
        private List<string> _startingKeys = new List<string>(); // Lista de inícios de frases válidos

        public void Train(List<ChatMessage> messages)
        {
            foreach (var message in messages)
            {
                if (string.IsNullOrWhiteSpace(message.Content)) continue;

                var words = CleanAndSplit(message.Content);

                if (words.Length < 3) continue; // Precisamos de pelo menos 3 palavras para formar um trio (key1, key2 -> value)

                // Adiciona o primeiro par de palavras à lista de inícios de frases
                _startingKeys.Add($"{words[0]} {words[1]}");

                for (int i = 0; i < words.Length - 2; i++)
                {
                    var key = $"{words[i]} {words[i + 1]}";
                    var nextWord = words[i + 2];

                    if (!_chain.ContainsKey(key))
                    {
                        _chain[key] = new List<string>();
                    }
                    _chain[key].Add(nextWord);
                }
            }
        }

        public string GenerateResponse(string seedPhrase, int maxLength = 25)
        {
            var words = CleanAndSplit(seedPhrase).ToList();
            if (words.Count < 2)
            {
                // Se a semente for muito curta, tenta a geração aleatória
                return GenerateRandomResponse(maxLength);
            }

            // Pega as duas últimas palavras como a semente inicial
            string currentKey = $"{words[words.Count - 2]} {words[words.Count - 1]}";

            if (!_chain.ContainsKey(currentKey))
            {
                // Se não conhecemos essa combinação, não podemos gerar uma resposta contextual
                return "Não sei o que dizer sobre isso...";
            }

            var responseBuilder = new StringBuilder();
            responseBuilder.Append(currentKey).Append(" ");

            for (int i = 0; i < maxLength; i++)
            {
                if (!_chain.ContainsKey(currentKey)) break;

                var possibleNextWords = _chain[currentKey];
                var nextWord = possibleNextWords[_random.Next(possibleNextWords.Count)];

                responseBuilder.Append(nextWord).Append(" ");

                // A nova chave é a segunda palavra da chave antiga + a nova palavra
                var keyParts = currentKey.Split(' ');
                currentKey = $"{keyParts[1]} {nextWord}";
            }

            return responseBuilder.ToString();
        }
        
        // --- FUNÇÃO MELHORADA ---
        public string GenerateRandomResponse(int maxLength = 20)
        {
            if (!_startingKeys.Any())
            {
                return "Ainda estou aprendendo, me ensine mais coisas!";
            }
            
            // Sorteia um início de frase válido
            var randomSeedKey = _startingKeys[_random.Next(_startingKeys.Count)];
            var seedParts = randomSeedKey.Split(' ');

            // Usa o GenerateResponse, que agora espera uma frase com pelo menos 2 palavras
            return GenerateResponse(randomSeedKey, maxLength);
        }

        // --- FUNÇÃO DE LIMPEZA MELHORADA ---
        private string[] CleanAndSplit(string text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<string>();

            // Converte para minúsculas
            var cleanedText = text.ToLower();
            // Remove URLs
            cleanedText = Regex.Replace(cleanedText, @"http[^\s]+", "");
            // Remove menções do Discord
            cleanedText = Regex.Replace(cleanedText, @"<@.?[0-9]+>", "");
            // Remove caracteres especiais, exceto espaços
            cleanedText = Regex.Replace(cleanedText, @"[^a-z0-9\sà-ú]", "", RegexOptions.IgnoreCase);
            // Remove espaços extras
            cleanedText = Regex.Replace(cleanedText, @"\s+", " ").Trim();
            
            return cleanedText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }
        
        // ... (as outras funções como FindClosestWord e LevenshteinDistance não precisam de alteração) ...
        // Se você as removeu, pode adicionar de volta se quiser a busca por palavras parecidas.
    }
}