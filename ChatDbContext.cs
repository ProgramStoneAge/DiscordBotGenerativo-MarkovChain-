using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// SPDX-License-Identifier: GPL-3.0 
// Copyright (c) 2025 Victor Pereira
namespace MeuBotGenerativo
{
    internal class ChatDbContext : DbContext
    {
        public DbSet<ChatMessage> Messages { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite("Data Source=chat_history.db");
    }
}
