using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.TextGeneration;
using System.Text;

namespace JC.SemanticKernel.Planners.UniversalLLMFunctionCaller
{
    public class UniversalLLMFunctionCaller
    {
        private Kernel _kernel;
        IChatCompletionService _chatCompletion;
        public UniversalLLMFunctionCaller(Kernel kernel)
        {
            _kernel = kernel;
            _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
        }

      public async Task<string> RunAsync(ChatHistory askHistory)
        {
            string ask = await GetAskFromHistory(askHistory);
            return await RunAsync(ask);
        }

        private async Task<string> GetAskFromHistory(ChatHistory askHistory)
        {
           StringBuilder sb = new StringBuilder();
            var userAndAssistantMessages = askHistory.Where(h => h.Role == AuthorRole.Assistant || h.Role == AuthorRole.User);
            foreach(var message in userAndAssistantMessages)
            {
                sb.AppendLine($"{message.Role.ToString()}: {message.Content}");
            }

            string extractAskFromHistoryPrompt = $@"Look at this dialog between a user and an assistant. 
I need to know what the user wants the assistant to do.
##Start of Conversation##
{sb.ToString()}
##End of Conversation##";

         var extractAskResult =  await _chatCompletion.GetChatMessageContentAsync(extractAskFromHistoryPrompt);
            string ask = extractAskResult.Content;
            return ask;
        }

        public async Task<string> RunAsync(string ask)
        {
            var plugins = _kernel.Plugins;
         var internalPlugin =   _kernel.Plugins.AddFromType<UniversalLLMFunctionCallerInternalFunctions>();

            string pluginsAsTextPrompt3000 = GetTemplatesAsTextPrompt3000(plugins);
           
            FunctionCall nextFunctionCall = new FunctionCall { Name = "Start" };
            int iterations = 0;
            ChatHistory history = new ChatHistory();
            history.Add(new ChatMessageContent(AuthorRole.User, "Start my spaceship"));
            history.Add(new ChatMessageContent(AuthorRole.Assistant, "GetMySpaceshipName()"));
            history.Add(new ChatMessageContent(AuthorRole.User, "MSS3000"));
            history.Add(new ChatMessageContent(AuthorRole.Assistant, "StartSpaceship(shipname: 'MSS3000')"));
            history.Add(new ChatMessageContent(AuthorRole.User, "Ship started"));

            while (iterations < 10 && nextFunctionCall.Name != "Finished")
            {
                for (int iRootRetry = 0; iRootRetry < 2; iRootRetry++)
                {
                    nextFunctionCall = await GetNextFunctionToCallAsync(ask, history, pluginsAsTextPrompt3000);
                    if (nextFunctionCall != null) break;
                }
                if (nextFunctionCall == null) throw new Exception("The LLM is not compatible with this approach.");

                string nextFunctionCallText = GetCallAsTextPrompt3000(nextFunctionCall);
                history.AddUserMessage(nextFunctionCallText);
              string pluginResponse = await InvokePluginAsync(nextFunctionCall);
                history.AddAssistantMessage(pluginResponse);
            }

            _kernel.Plugins.Remove(internalPlugin);
            if(nextFunctionCall.Name == "Finished")
            {
                string finalMessage = nextFunctionCall.Parameters[0].Value.ToString();
                return finalMessage;
            }
            throw new Exception("LLM could not finish workflow within 10 steps. consider increasing the number of steps"); 
        }

        private async Task<FunctionCall> GetNextFunctionToCallAsync(string ask, ChatHistory history, string pluginsAsTextPrompt3000)
        {
            StringBuilder sbhistory = new StringBuilder();
            foreach (var item in history)
            {
                if (item.Role == AuthorRole.Assistant)
                {
                    sbhistory.AppendLine("Computer: " + item.Content);
                } else
                {
                    sbhistory.AppendLine("User: " + item.Content);
                }
            }

            string nextFunctionCallPrompt = $@"You are an advisor, that is tasked with finding the next step to fullfil a goal.
Below, you are provided a goal, that needs to be reached, and a chat between a user and a computer, as well as a list of functions that the user could use.
You need to find out what the next step for the user is to reach the goal. You are also provided a list of functions that are in TextPrompt3000 Schema Format.
The TextPrompt3000 Format is defined like this:
{GetTextPrompt300Explanation()}
##goal##
{ask}. Afterwards, use the 'Finished' function
##end goal##
##history##
{sbhistory.ToString()}
##end history##
##available functions##
{pluginsAsTextPrompt3000}
##end functions##

The following rules are very important:
1) you can only recommend one function and the parameters, not multiple functions
2) You can only recommend a function that is in the list of available functions
3) You need to give all parameters for the function
4) Given the history, the function you recommend needs to be important to get closer towards the goal
5) Do not wrap functions into each other. Stick to the list of functions, this is not a math problem. We only need one function, the next one needed
6) Do not recommend a function that was recently called. Use the output instead. Do not use Placeholders or Functions as parameters for other functions
7) When all  necessary functions are called and the result was presented by the computer system, call the Finished function and present the result


If you break any of those rules, a kitten dies. 
What function should the user execute next on the computer? Explain your reasoning step by step.
";

            //we are doing multi-tapping here. First get an elaborate answer, and then press that answer into a usable format
            var tap1Answer = await _chatCompletion.GetChatMessageContentAsync(nextFunctionCallPrompt);
            string tap2Prompt = @$"You are a computer system. You can only answer with a TextPrompt3000 function, nothing else. 
A user is giving you a text. You need to extract a TextPrompt3000 function call from it.
{GetTextPrompt300Explanation()}
You can not say anything else but a function call. Do not explain anything. Do not answer as a schema, including '--' in the end. 
Answer as actual function call. The result could look like this 'FunctionA(paramA: content)' or 'FunctionB()'
##Text from user##
{tap1Answer.Content}
##
##Available functions#
{pluginsAsTextPrompt3000}
##
It is very important that you only return a valid function that you extracted from the users text, no other text. 
Do not supply multiple functions, only one, the next necessary one.
It needs to include all necessary parameters. 
If you do not do this, a very cute kitten dies.";

            ChatHistory tap2History = new ChatHistory();
            tap2History.AddUserMessage(tap2Prompt);

            
            for(int iRetry = 0; iRetry < 5; iRetry++) {
                var tap2Answer = await _chatCompletion.GetChatMessageContentsAsync(tap2History);
                Console.WriteLine(tap2Answer[0].Content);   
                tap2History.AddAssistantMessage(tap2Answer[0].Content);
            try
                {
                    FunctionCall functionCall = ParseTextPrompt3000Call(tap2Answer);
                    ValidateFunctionWithKernel(functionCall);

                    return functionCall;
                } catch (Exception ex)
                {
                    tap2History.AddUserMessage($"Error: '{ex.Message}'. Try again. Do not apologise. Do not explain anything. just follow the rules from earlier");
                }
            }

            return null;
        }

        private void ValidateFunctionWithKernel(FunctionCall functionCall)
        {
            // Iterate over each plugin in the kernel
            foreach (var plugin in _kernel.Plugins)
            {
                // Check if the plugin has a function with the same name as the function call
                var function = plugin.FirstOrDefault(f => f.Name == functionCall.Name);
                if (function != null)
                {
                    // Check if the function has the same parameters as the function call
                    if (function.Metadata.Parameters.Count == functionCall.Parameters.Count)
                    {
                        for (int i = 0; i < function.Metadata.Parameters.Count; i++)
                        {
                            if (function.Metadata.Parameters[i].Name != functionCall.Parameters[i].Name)
                            {
                                throw new Exception("Parameter " + functionCall.Parameters[i].Name + " does not exist in the function.");
                            }
                        }
                        return; // Exit the function if both function name and parameters match
                    }
                    else
                    {
                        throw new Exception($"Parameter count does not match for the function.");
                    }
                }
            }

            throw new Exception($"Function '{functionCall.Name}' does not exist in the kernel.");
        }



        private FunctionCall ParseTextPrompt3000Call(IReadOnlyList<ChatMessageContent> tap2Answer)
        {
            try
            {
                // Get the content of the first ChatMessageContent
                string content = tap2Answer[0].Content;

                // Split the content into function name and parameters
                int openParenIndex = content.IndexOf('(');
                int closeParenIndex = content.IndexOf(')');
                string functionName = content.Substring(0, openParenIndex);
                string parametersContent = content.Substring(openParenIndex + 1, closeParenIndex - openParenIndex - 1);

                // Create a new FunctionCall
                FunctionCall functionCall = new FunctionCall();
                functionCall.Name = functionName;
                functionCall.Parameters = new List<FunctionCallParameter>();

                // Check if there are any parameters
                if (!string.IsNullOrWhiteSpace(parametersContent))
                {
                    // Split the parameters by comma
                    string[] parameters = parametersContent.Split(',');

                    // Parse each parameter
                    foreach (string parameter in parameters)
                    {
                        // Split the parameter into name and value
                        string[] nameAndValue = parameter.Split(':');
                        string parameterName = nameAndValue[0].Trim();
                        object parameterValue = nameAndValue[1].Trim(); // You might want to convert this to the appropriate type

                        // Create a new FunctionCallParameter and add it to the FunctionCall
                        FunctionCallParameter functionCallParameter = new FunctionCallParameter();
                        functionCallParameter.Name = parameterName;
                        functionCallParameter.Value = parameterValue;
                        functionCall.Parameters.Add(functionCallParameter);
                    }
                }

                return functionCall;
            }catch(Exception ex)
            {
                throw new Exception("There was an error parsing the result. Make sure you adher to all rules and only return actually existing Functions in the correct TextPrompt3000 format, without wrapping Functions");
            }
        }



        private string GetTextPrompt300Explanation()
        {
            return $@"##example of TextPrompt3000##
FunctionName(parameter: value)
##
In TextPrompt3000, there are also schemas. 
##Example of TextPrompt3000 Schema##
FunctionName(datatype1 parametername1:'parameter1 description',datatype2 parametername2:'parameter2 description')--Description of the function
##
There can be no parameters, one, or many parameters.
For example, a Schema looking like this:
StartSpaceship(string shipName:'The name of the starting ship', int speed: 100)--Starts a specific spaceship with a specific speed
would be called like this:
StartSpaceship(shipName: 'Enterprise', speed: 99999)
";
        }

        private string GetCallAsTextPrompt3000(FunctionCall nextFunctionCall)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(nextFunctionCall.Name);
            sb.Append("(");
            for (int i = 0; i < nextFunctionCall.Parameters.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(nextFunctionCall.Parameters[i].Name);
                sb.Append(": ");
                sb.Append(nextFunctionCall.Parameters[i].Value.ToString());
            }
            sb.Append(")");
            return sb.ToString();
        }


        private async Task<string> InvokePluginAsync(FunctionCall functionCall)
        {
           
            // Iterate over each plugin in the kernel
            foreach (var plugin in _kernel.Plugins)
            {
                // Check if the plugin has a function with the same name as the function call
                var function = plugin.FirstOrDefault(f => f.Name == functionCall.Name);
                if (function != null)
                {
                    // Create a new context for the function call
                    KernelArguments context = new KernelArguments();

                    // Add the function parameters to the context
                    foreach (var parameter in functionCall.Parameters)
                    {
                        context[parameter.Name] = parameter.Value;
                    }

                    // Invoke the function
                    var result = await function.InvokeAsync(_kernel,context);

                    return result.ToString();
                }
            }

            throw new Exception("Function does not exist in the kernel.");
        }


        private string GetTemplatesAsTextPrompt3000(KernelPluginCollection plugins)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var plugin in plugins)
            {
                foreach (var functionimpl in plugin)
                {
                    var function = functionimpl.Metadata;
                    sb.Append(function.Name);
                    sb.Append("(");
                    for (int i = 0; i < function.Parameters.Count; i++)
                    {
                        if (i > 0)
                            sb.Append(", ");
                        sb.Append(function.Parameters[i].ParameterType.Name);
                        sb.Append(" ");
                        sb.Append(function.Parameters[i].Name);
                        sb.Append(": '");
                        sb.Append(function.Parameters[i].Description);
                        sb.Append("'");
                    }
                    sb.Append(")");
                    sb.Append("--");
                    sb.Append(function.Description);
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }


    }
}
