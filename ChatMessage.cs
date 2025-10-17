using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// SPDX-License-Identifier: GPL-3.0 
// Copyright (c) 2025 Victor Pereira

namespace MeuBotGenerativo
{
    internal class ChatMessage
    {
        public ulong Id { get; set; } // ID da mensagem do Discord
        public ulong AuthorId { get; set; }
        public string AuthorUsername { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
