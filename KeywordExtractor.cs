// KeywordExtractor.cs
using System;
using System.Collections.Generic;
using System.Linq;
// SPDX-License-Identifier: GPL-3.0 
// Copyright (c) 2025 Victor Pereira
namespace MeuBotGenerativo
{
    internal class KeywordExtractor
    {
        // Dicionário para guardar a "raridade" de cada palavra (IDF)
        private readonly Dictionary<string, double> _inverseDocumentFrequency = new Dictionary<string, double>();
        private readonly int _totalDocuments;

        /// <summary>
        /// Construtor que "treina" o modelo de IDF com todo o histórico de mensagens.
        /// </summary>
        public KeywordExtractor(List<ChatMessage> allMessages)
        {
            if (allMessages == null || !allMessages.Any()) return;

            _totalDocuments = allMessages.Count;
            var wordDocumentCounts = new Dictionary<string, int>();

            // Conta em quantos documentos (mensagens) cada palavra aparece
            foreach (var message in allMessages)
            {
                var uniqueWordsInMessage = message.Content.ToLower().Split(' ')
                    .Where(w => !string.IsNullOrWhiteSpace(w))
                    .Distinct();

                foreach (var word in uniqueWordsInMessage)
                {
                    if (!wordDocumentCounts.ContainsKey(word))
                    {
                        wordDocumentCounts[word] = 0;
                    }
                    wordDocumentCounts[word]++;
                }
            }

            // Calcula o score de IDF para cada palavra
            foreach (var entry in wordDocumentCounts)
            {
                _inverseDocumentFrequency[entry.Key] = Math.Log((double)_totalDocuments / (1 + entry.Value));
            }

            Console.WriteLine($"[LOG] Extrator de Palavras-Chave treinado com {_inverseDocumentFrequency.Count} palavras únicas.");
        }

        /// <summary>
        /// Extrai as palavras-chave mais importantes de um texto.
        /// </summary>
        public List<string> ExtractKeywords(string text, int topN = 3)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();

            var words = text.ToLower().Split(' ').Where(w => !string.IsNullOrWhiteSpace(w)).ToList();
            if (!words.Any()) return new List<string>();

            // Calcula a frequência de cada palavra no texto (TF)
            var termFrequency = words
                .GroupBy(w => w)
                .ToDictionary(g => g.Key, g => (double)g.Count() / words.Count);

            // Calcula o score final TF-IDF para cada palavra
            var tfidfScores = new Dictionary<string, double>();
            foreach (var word in termFrequency.Keys)
            {
                if (_inverseDocumentFrequency.ContainsKey(word))
                {
                    tfidfScores[word] = termFrequency[word] * _inverseDocumentFrequency[word];
                }
            }

            // Retorna as N palavras com maior score
            return tfidfScores.OrderByDescending(kv => kv.Value).Take(topN).Select(kv => kv.Key).ToList();
        }
    }
}