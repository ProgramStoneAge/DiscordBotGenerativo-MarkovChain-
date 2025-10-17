# MeuBotGenerativo — Bot de Discord (ML.NET + Markov + Gemini)

Bot híbrido que combina **classificação de intenção com ML.NET**, **geração de texto por Cadeias de Markov** e, para perguntas, um **fallback com Google Gemini**. O histórico é persistido em **SQLite** e serve tanto para treinar as personalidades (Markov) quanto para extrair palavras‑chave (TF‑IDF) e melhorar as respostas.

---

## Sumário

* [Arquitetura](#arquitetura)
* [Requisitos](#requisitos)
* [Instalação das dependências](#instalação-das-dependências)
* [Configurando chaves e variáveis de ambiente](#configurando-chaves-e-variáveis-de-ambiente)
* [Criando o dataset (TSV) — 2 colunas](#criando-o-dataset-tsv--2-colunas)
* [Treinando o modelo (primeira execução)](#treinando-o-modelo-primeira-execução)
* [Banco de dados SQLite](#banco-de-dados-sqlite)
* [Configuração no Discord Developer Portal](#configuração-no-discord-developer-portal)
* [Executando o bot](#executando-o-bot)
* [Como o bot decide as respostas](#como-o-bot-decide-as-respostas)
* [Erros comuns e soluções](#erros-comuns-e-soluções)

---

## Arquitetura

* **Classificador de intenções (ML.NET):** lê um TSV com duas colunas (`Intencao` e `Frase`), treina um modelo e salva em `intent_model.zip`.
* **Gerador de personalidade (Markov):** treina "cérebros" por intenção com cadeias de Markov de ordem 2 (usa pares de palavras como chave) e gera texto com *seed* por palavras‑chave.
* **Extrator de palavras‑chave (TF‑IDF):** calcula IDF com base no histórico do banco e usa TF‑IDF da mensagem para sugerir *seeds* ao Markov.
* **Fallback de conhecimento (Gemini):** para mensagens classificadas como "pergunta", envia o histórico recente para a API do Gemini e retorna a resposta.
* **Persistência:** o histórico é salvo em `chat_history.db` (SQLite) com a tabela `Messages`.

---

## Requisitos

* **.NET SDK 7+** (8 recomendado)
* **NuGet packages**:

  * `Discord.Net`
  * `Microsoft.EntityFrameworkCore`
  * `Microsoft.EntityFrameworkCore.Sqlite`
  * `Microsoft.ML`
  * `Newtonsoft.Json`

> Opcional (útil para migrações): `Microsoft.EntityFrameworkCore.Design`

### Instalar globalmente (no diretório do projeto)

```bash
dotnet restore

dotnet add package Discord.Net
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.ML
dotnet add package Newtonsoft.Json
```

---

## Configurando chaves e variáveis de ambiente

O código usa duas chaves:

1. **DISCORD_TOKEN** — Token do bot no Discord.
2. **GEMINI_API_KEY** — Chave da API do Google Generative Language (modelo `gemini-1.5-flash-latest`).

### Opção A — Variáveis de ambiente (recomendado)

Defina as variáveis no seu terminal/ambiente:

```bash
# Linux/macOS
export DISCORD_TOKEN="seu_token_do_discord"
export GEMINI_API_KEY="sua_chave_do_gemini"

# Windows (PowerShell)
$env:DISCORD_TOKEN="seu_token_do_discord"
$env:GEMINI_API_KEY="sua_chave_do_gemini"
```

> **Dica:** você pode alterar o código para ler as env vars, por exemplo:
> `var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN") ?? "";`
> `private static readonly string GeminiApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";`

### Opção B — Colar direto no código (rápido, porém menos seguro)

* Em `Program.cs`, atribua o token do Discord e a `GeminiApiKey` diretamente nas variáveis.

---

## Criando o dataset (TSV) — 2 colunas

Crie um arquivo chamado **`dados_ia_30.tsv`** (com *header*) contendo **duas colunas**:

* `Intencao` — rótulo da classe (ex.: `saudacao`, `pergunta`, `agradecimento`, `afirmacao`, `despedida`…)
* `Frase` — exemplo de texto para aquela intenção

Exemplo mínimo:

```
Intencao	Frase
saudacao	olá, tudo bem?
pergunta	como uso o comando x?
afirmacao	ok, entendi
agradecimento	valeu pela ajuda!
despedida	até mais!
```

> Quanto mais linhas por intenção, melhor.

---

## Treinando o modelo (primeira execução)

Como você apagou o `.tsv` e/ou o `intent_model.zip`, é preciso **regenerar** o modelo.

### Etapa 1 — Garanta que o TSV existe

* Confirme que **`dados_ia_30.tsv`** está no diretório do executável/projeto e tem as **duas colunas** acima.

### Etapa 2 — Chame `Train()` na primeira execução

Você tem **duas formas** (escolha **uma**) para chamar `ModelTrainer.Train()` na primeira vez:

#### ✔ Opção 1 (mais simples): chamar no `Main` **antes** de criar o `Program`

Substitua seu `Main` por algo assim:

```csharp
public static Task Main(string[] args)
{
    if (!File.Exists("intent_model.zip"))
    {
        ModelTrainer.Train(); // Gera intent_model.zip
    }
    return new Program().MainAsync();
}
```

*Vantagem:* mantém o restante do código igual e garante que o modelo exista **antes** de ser carregado.

#### Opção 2: chamar no `MainAsync` (conforme seu pedido)

Requer pequenas mudanças:

1. Troque o campo do *prediction engine* para não ser `readonly`.
2. Carregue o modelo **depois** de chamar `Train()`.

Exemplo:

```csharp
// 1) Mude o campo
private PredictionEngine<ModelInput, ModelOutput> _intentPredictionEngine;

public async Task MainAsync()
{
    // Cria o banco/tabelas se não existirem (veja seção de Banco de Dados)
    _dbContext.Database.EnsureCreated();

    // 2) Treina se for a primeira vez
    if (!File.Exists("intent_model.zip"))
    {
        ModelTrainer.Train();
    }

    // 3) Só agora carregue o modelo
    var mlContext = new MLContext();
    var trainedModel = mlContext.Model.Load("intent_model.zip", out _);
    _intentPredictionEngine = mlContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(trainedModel);

    // ... seguir com o restante do startup (Discord login etc.)
}
```

> **Importante:** se no seu construtor você já carrega o modelo, mova esse trecho para depois do `Train()` (como mostrado acima), ou então use a **Opção 1**.

---

## Banco de dados SQLite

* O bot usa **SQLite** (`chat_history.db`).
* Para primeira execução (ou se você apagou o banco), **garanta a criação das tabelas** chamando:

```csharp
_dbContext.Database.EnsureCreated();
```

Coloque isso no início do `MainAsync()` (ou no construtor) **antes** de qualquer leitura/escrita. O histórico será salvo automaticamente conforme as mensagens chegam.

> Se preferir migrações, adicione `Microsoft.EntityFrameworkCore.Design` e configure normalmente.

---

## Configuração no Discord Developer Portal

1. Acesse **[https://discord.com/developers/applications](https://discord.com/developers/applications)** → *New Application* → *Create*.
2. Aba **Bot** → *Add Bot* → *Reset Token* → **copie o token** (use em `DISCORD_TOKEN`).
3. **Privileged Gateway Intents**: habilite **Message Content Intent**. (Guild Members não é necessário para este bot)
4. Aba **OAuth2 → URL Generator**:

   * Scopes: **bot**
   * Bot Permissions: **Read Messages/View Channels**, **Send Messages**, **Read Message History** (o essencial para este bot)
   * Copie a URL gerada, visite e **adicione o bot no seu servidor**.

> O código liga o cliente com `GatewayIntents` que incluem `MessageContent`, então a flag **precisa** estar habilitada no portal.

---

## Executando o bot

```bash
dotnet build
DOTNET_ENVIRONMENT=Production dotnet run
```

> Verifique no console se logou com sucesso e se conectou ao gateway do Discord.

---

## Como o bot decide as respostas

1. **Classifica a intenção** da mensagem usando o modelo treinado.
2. Se for **`pergunta`**: monta um **histórico recente** e chama o **Gemini** (`gemini-1.5-flash-latest`).
3. Caso contrário (saudação/afirmação/agradecimento/despedida ou desconhecida):

   * Extrai **palavras‑chave** da mensagem via **TF‑IDF**;
   * Usa um **cérebro Markov** específico daquela intenção para gerar a resposta, tentando primeiro com *seeds*; se não houver contexto, gera uma resposta aleatória (mas plausível) a partir do que já aprendeu no seu servidor.
4. O bot **somente responde quando for mencionado** (`@SeuBot`) e **salva tudo** no banco para continuar aprendendo.
