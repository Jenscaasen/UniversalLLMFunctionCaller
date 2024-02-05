# UniversalLLMFunctionCaller

The UniversalLLMFunctionCaller automatically invokes functions until a given goal or workflow is done. This works without the LLM natively supporting FunctionCalling (OpenAI), which therefore enables other LLM providers as Mistral, Anthropic, Meta, Google and others to also use function calling.
Usage example with Mistral:

```C#
 IKernelBuilder builder = Kernel.CreateBuilder();
 builder.AddMistralChatCompletion(Environment.GetEnvironmentVariable("mistral_key"), "mistral-small");
 var kernel = builder.Build();
 kernel.ImportPluginFromType<TimePlugin>("Time");
 kernel.ImportPluginFromType<MathPlugin>("Math");

 UniversalLLMFunctionCaller planner = new(kernel);
 string ask = "What is the current hour number, plus 5?";
 Console.WriteLine(ask);
 string result = await planner.RunAsync("What is the current hour number, plus 5?");
 Console.WriteLine(result);
```

This works with a single ask, but also supports a ChatHistory:

```C#
[...]
UniversalLLMFunctionCaller planner = new(kernel);

ChatHistory history = new ChatHistory();
history.AddUserMessage("What is the capital of france?");
history.AddAssistantMessage("Paris");
history.AddUserMessage("And of Denmark?");
 result = await planner.RunAsync(history);
```
Here, the plugin reads the context of the ChatHistory to understand what the actual question is ("What is the capital of Denmark?") instead of just using an out-of-context message ("And of Denmark?")

According to internal tests, this Planner is both faster and more reliable compared to the Handlebar Planner, that offers a similar functionality out of the box for non-OpenAI LLMs.
Tested with Mistral (nuget: JC.SemanticKernel.Connectors.Mistral). More testing with more use cases and more LLMs needed. Feel free to create issues and do pull requests.

The Plugins in the Demo and Test Project are taken from the Main Semantic Kernel Repo: https://github.com/microsoft/semantic-kernel 
I do not claim ownership or copyright on them.

## FAQ

### Why don't you use JSON?
<details>
<summary>Click to expand!</summary>

The completely made up standard "TextPrompt3000" needs less tokens and is therefore faster and cheaper, especially if you have many Plugins registered. The algorithm relies on retries and telling the LLM their mistakes. This is to mitigate high costs and long runs.

</details>

### It doesn't work with my LLM. What should i do?
<details>
<summary>Click to expand!</summary>

Please create an issue on Github and share as many information as possible: what was the task, what plugins were used, what did the LLM respond?

</details>

### Does it realy work with all LLMs?
<details>
<summary>Click to expand!</summary>

No. For Mistral, the medium and small models work. The Tiny Model seems to lack a basic understanding of planning, and does not move forward in the process. An undefined minimum of cleverness needs to reside in the LLM for this to work

</details>
