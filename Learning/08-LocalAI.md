# Chapter 8 — Local AI

> **Goal:** Run the same ideas from the earlier chapters on this machine.
> A local model is a file of weights plus a runtime. The question does not
> have to leave the computer.
>
> The four pieces are **Ollama, ONNX, local embeddings, and local LLMs.**

---

## Table of Contents

1. [Cloud and local](#1-cloud-and-local)
2. [Ollama](#2-ollama)
3. [ONNX](#3-onnx)
4. [Local embeddings](#4-local-embeddings)
5. [Local LLMs](#5-local-llms)
6. [Common Pitfalls](#6-common-pitfalls)
7. [Practice Exercises](#7-practice-exercises)

> **Settings for this chapter** live in the `Ollama` section of
> `appsettings.json`. No OpenAI key is needed for menu option 12.
>
> | Setting | Default | Used for |
> | --- | --- | --- |
> | `Ollama:BaseUrl` | `http://127.0.0.1:11434` | Where `OllamaClient` sends requests (must be this machine) |
> | `Ollama:ChatModel` | `llama3.2` | Preferred chat model for options 4 and 5 |
> | `Ollama:EmbeddingModel` | `nomic-embed-text` | Preferred embedding model for options 3 and 4 |
> | `Ollama:RequestTimeoutSeconds` | 120 | HTTP timeout; a first call on CPU can be slow while the model loads |

---

## 1. Cloud and local

Chapters 1 to 7 call `api.openai.com`. The weights sit in OpenAI's data center.
You send the prompt, they send the tokens, you pay the price table from chapter 6.

A local model reverses that:

```
Your process
   ↓
Runtime on this CPU or GPU
   ↓
Weights in a file on this disk
   ↓
Answer
```

The chat shape does not change. You still send messages and read a reply.
What changes is the host, the file format, and the fact that a missing
model is a file you do not have yet, not an API key.

Menu option **12**. Options **2** and **3** never use the network.
`LocalAiChecks.Verify()` locks those two before the menu. Ollama is optional.

---

## 2. Ollama

Ollama is a local server. It loads a **GGUF** file and exposes a small HTTP API
on `127.0.0.1:11434`. `OllamaClient` calls that API. It does not call OpenAI.

Option **1** asks the server which models are installed.

### Install Ollama on Windows

Open PowerShell and install the app:

```powershell
winget install Ollama.Ollama
```

If `winget` is not available, download the Windows installer from
https://ollama.com/download and run it.

Close the terminal and open a new one so `ollama` is on the path.
The installer also starts Ollama in the system tray. The llama icon means
the server is listening. You do not type `ollama serve` on Windows unless
that icon is missing.

Check the program:

```powershell
ollama --version
```

### Download the two models

`llama3.2` is the chat model. `nomic-embed-text` is the embedding model.
They are different files. A chat model is a poor embedder, and an embedder
does not write answers. These two names are the defaults for
`Ollama:ChatModel` and `Ollama:EmbeddingModel` in `appsettings.json`.

```powershell
ollama pull llama3.2
ollama pull nomic-embed-text
```

`pull` downloads the GGUF weights into Ollama's local store. The first
download is about 2 GB for `llama3.2` and about 270 MB for `nomic-embed-text`.
Confirm both names are listed:

```powershell
ollama list
```

You should see `llama3.2:latest` and `nomic-embed-text:latest`.

Try the chat model in the terminal before using this app:

```powershell
ollama run llama3.2
```

Type a sentence. Type `/bye` to leave. That chat never called OpenAI.

### Connect this app

`OllamaClient` reads its address from `Ollama:BaseUrl` and sends no API key:

```
http://127.0.0.1:11434
```

`LocalAiChecks` refuses to start the chapter if `Ollama:BaseUrl` is not a
loopback address (`127.0.0.1` or `localhost`). Pointing it at another
machine would send your prompts over the network, which defeats the point
of this chapter.

The app looks for the configured model names first. If they are not
installed, it falls back to any installed chat model (a name without
`embed`) or embedding model (a name with `embed`). To try a different
model, `ollama pull` it and change `Ollama:ChatModel` or
`Ollama:EmbeddingModel`. No code change is needed.

Leave the tray icon running. Start this app and choose menu option **12**.

| Step | What you do |
| --- | --- |
| Option **1** | Confirms the server is up and prints `llama3.2` and `nomic-embed-text`. |
| Option **5** | Chats with `llama3.2`. Each reply streams to the console. Type `exit` to leave. |

If option 1 says Ollama is not running, start **Ollama** from the Start menu
and try again. The models stay on disk. You do not `pull` them a second time.

If a chat model is installed, option **5** is a conversation with `llama3.2`.
Each turn sends the messages so far, because Ollama does not keep them for you.
The request uses `stream: true`, and the console prints each chunk as it arrives.
Type `exit` to leave. The request stays on this machine. Quit Ollama and that
call fails. The cloud call in the other chapters does not.

---

## 3. ONNX

ONNX is a file format for a model graph, and ONNX Runtime is the program
that executes that graph on CPU or GPU.

The demo cannot ship a 90 MB embedding model, so option **2** runs the same
idea at a size you can read. `LocalWeightModel` is:

```
y = W x + b
```

with

```
W = | 1  0 |
    | 0  2 |
b = [0.5, -1]
x = [3, 4]
y = [3.5, 7]
```

Option 2 writes those weights to `tiny-linear.json`, loads them back, and
runs the multiply on this CPU. The file is the model. The C# method is the runtime.

A production `.onnx` file swaps the loader, not the idea:

```csharp
using var session = new InferenceSession("model.onnx");
var results = session.Run(inputs);
```

A GPU build uses a different execution provider. `Run` stays the call you make.
Q4 and FP32 are how densely the weights are stored. Fewer bits, smaller file,
some loss of quality. That trade is model selection.

---

## 4. Local embeddings

Chapter 3 embeds text by calling OpenAI, then ranks chunks with cosine similarity.
The rank step does not care who made the vector. The vector step can be local.

Option **3** builds a `BagEmbedder` from the four local documents.
Each document becomes a count of its own words, normalized to length 1.
The question `"how many paid vacation days"` is embedded with that same vocabulary.

The vacation line shares `paid`, `vacation`, and `days`. The snack line does not.
Cosine puts `hr-vacation` first. `LocalAiChecks` fails startup if that stops being true.

Two rules carry over from chapter 3:

- Embed the documents and the question with the **same** model.
- A vector from OpenAI and a vector from this bag-of-words model are not comparable.

`nomic-embed-text` in Ollama, or `all-MiniLM-L6-v2` as an ONNX file, replaces
the bag-of-words when you want meaning and not just shared words. The search
code stays `Cosine`.

---

## 5. Local LLMs

Option **4** starts with a model card. Pick by the job and by the RAM you have.

| Model | File | Quantization | Memory | Use |
| --- | --- | --- | --- | --- |
| llama3.2 | GGUF | Q4_K_M | about 2 GB | Chat on a laptop |
| llama3.1:8b | GGUF | Q4_K_M | about 5 GB | Stronger chat |
| nomic-embed-text | GGUF | F16 | about 300 MB | Embeddings via Ollama |
| all-MiniLM-L6-v2 | ONNX | FP32 | about 90 MB | Embeddings via ONNX Runtime |

Q4 stores a weight in about 4 bits. The 8B model at full precision would not
fit the "about 5 GB" line. Quantization is what makes the laptop copy possible.

Under the table, the demo answers with no generator at all: embed the question
locally and return the nearest document, with its id in brackets. That is a
local RAG answer. It cannot rephrase. It also cannot invent a December payout.

If Ollama is up and a chat model is installed, you can hand that same document
to the local model and ask for one sentence. The document is the context.
The weights are local. The guards from chapter 7 still apply: a local model
will follow a poisoned document if you paste the document in unfiltered.

---

## 6. Common Pitfalls

| Pitfall | What to do instead |
| --- | --- |
| Treating Ollama as "no security needed" | Chapter 7 still runs. Local only changes where the weights are. |
| One model for chat and for embeddings | Pull a chat model and an embedding model. They are different files. |
| Mixing OpenAI vectors with local vectors | One model embeds both the corpus and the query. |
| Downloading the biggest GGUF first | Read the memory column. Q4 is the laptop default. |
| Expecting the bag-of-words demo to match MiniLM | It counts shared words. A real local embedder is the ONNX or Ollama file. |
| Calling the cloud out of habit | Option 1 prints the host. `127.0.0.1` is the check. |

---

## 7. Practice Exercises

All of these run from menu option **12**.

### Exercise 1 — See whether the server is local

Option **1**.

If Ollama is down, the message names `127.0.0.1` and the two `ollama pull` commands.
Start Ollama, pull `llama3.2`, run option **5**, and say hello.
Ask a follow-up. The second reply can use the first turn because the app
sends the history. That reply did not go to OpenAI.

If you have the RAM, run `ollama pull llama3.1:8b`, set `Ollama:ChatModel`
to `llama3.1:8b` in `appsettings.json`, and run option **5** again. Same
code, different weights file. Compare the answer quality and how long the
first reply takes.

### Exercise 2 — A model is a file

Option **2**.

Check the printed output against the formula: `[3, 4]` goes in, `[3.5, 7]` comes out.
Open the path printed for `tiny-linear.json`. Those numbers are the weights.
The `InferenceSession` lines are what you use when the file ends in `.onnx`.

### Exercise 3 — Rank with local vectors

Option **3**.

Confirm `hr-vacation` has the highest cosine for "paid vacation days".
Confirm `office-snacks` is not first. The vectors were built in this process.

### Exercise 4 — Pick a model, then answer

Option **4**.

Read the memory column before the name. `llama3.2` at Q4 is the laptop chat model.
`nomic-embed-text` is not a chat model.

Read the extractive answer. It is the vacation sentence plus `[hr-vacation]`.
If Ollama is up, say yes and compare that sentence with the local model's sentence.
Both had the same document. Only the second one used a generative model.

---

## What's Next?

The roadmap's next topic, after these eight chapters, is multi-agent systems: a supervisor and specialist
agents sharing state. Chapter 4 already ran one version of that. A local model
can sit inside one of those agents. The trace from chapter 6 should still show
which model file answered, and the guards from chapter 7 should still run
before a tool does.
