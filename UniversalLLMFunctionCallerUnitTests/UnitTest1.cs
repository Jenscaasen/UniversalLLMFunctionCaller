using JC.SemanticKernel.Planners.UniversalLLMFunctionCaller;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using Microsoft.SemanticKernel.Plugins.Web;
using SemanticKernel.IntegrationTests.Fakes;
using UniversalLLMFunctionCallerDemo.Plugins;
using Google.Apis.CustomSearchAPI.v1.Data;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JC.SemanticKernel.Planners.UniversalLLMFunctionCaller.UniversalLLMFunctionCallerUnitTests
{
    [TestClass]
    public class UniversalLLMFunctionCallerUnitTests
    {
        [TestMethod]
        public async Task TestFromChatHistory()
        {
            IKernelBuilder builder = Kernel.CreateBuilder();
            builder.AddMistralChatCompletion(Environment.GetEnvironmentVariable("mistral_key"), "mistral-small");
            var kernel = builder.Build();
            string bingApiKey = Environment.GetEnvironmentVariable("bing_key");
            var bingConnector = new BingConnector(bingApiKey);
            var webSearchEnginePlugin = new WebSearchEnginePlugin(bingConnector);
            kernel.ImportPluginFromObject(webSearchEnginePlugin, "WebSearch");
            
            UniversalLLMFunctionCaller planner = new(kernel);

            ChatHistory history = new ChatHistory();
            history.AddSystemMessage("You are a web searching assistant. You solve everything by searching the web for information");
            history.AddUserMessage("What is the capital of france?");
            
            string result = await planner.RunAsync(history);
            history.AddAssistantMessage(result);
            history.AddUserMessage("And of Denmark?");
             result = await planner.RunAsync(history);
            Assert.IsTrue(result.Contains("Copenhagen"));
        }

        [TestMethod]
        public async Task ConvertStringSingleStepTestAsync()
        {
            IKernelBuilder builder = Kernel.CreateBuilder();
            builder.AddMistralChatCompletion(Environment.GetEnvironmentVariable("mistral_key"), "mistral-small");
            var kernel = builder.Build();
            kernel.ImportPluginFromType<TextPlugin>("Text");

            UniversalLLMFunctionCaller planner = new(kernel);
            string ask = "Make this text upper case: hello i want to grow please";

            string result = await planner.RunAsync(ask);
            Assert.IsTrue(result.Contains("HELLO"));
        }
        [TestMethod]
        public async Task AddHoursMultiStepTestAsync()
        {
            IKernelBuilder builder = Kernel.CreateBuilder();
            builder.AddMistralChatCompletion(Environment.GetEnvironmentVariable("mistral_key"), "mistral-small");
            var kernel = builder.Build();
            kernel.ImportPluginFromType<TimePlugin>("Time");
            kernel.ImportPluginFromType<MathPlugin>("Math");

            UniversalLLMFunctionCaller planner = new(kernel);
            string ask = "What is the current hour number, plus 5?";
        
            string result = await planner.RunAsync(ask);
            string correctAnswer = (DateTime.Now.Hour + 5).ToString();
            var containsCorrectAnswer = result.Contains(correctAnswer);
            Assert.IsTrue(containsCorrectAnswer);
        }

        [TestMethod]
        public async Task WebSearchTest()
        {
            IKernelBuilder builder = Kernel.CreateBuilder();
            builder.AddMistralChatCompletion(Environment.GetEnvironmentVariable("mistral_key"), "mistral-small");           
            var kernel = builder.Build();
            string bingApiKey = Environment.GetEnvironmentVariable("bing_key");
            var bingConnector = new BingConnector(bingApiKey);
            var webSearchEnginePlugin = new WebSearchEnginePlugin(bingConnector);
            kernel.ImportPluginFromObject(webSearchEnginePlugin, "WebSearch");

            UniversalLLMFunctionCaller planner = new(kernel);
            string ask = "What is the tallest mountain on Earth? How tall is it in meters?";

            string result = await planner.RunAsync(ask);

            Assert.IsTrue(result.Contains("Everest") && (result.Contains("8848") || result.Contains("8.848") || result.Contains("8,848")));
        }
        [TestMethod]
        public async Task CalcAndMailTest()
        {
            IKernelBuilder builder = Kernel.CreateBuilder();
            builder.AddMistralChatCompletion(Environment.GetEnvironmentVariable("mistral_key"), "mistral-small");
          
            var kernel = builder.Build();

            kernel.ImportPluginFromType<MathPlugin>("Math");
            kernel.ImportPluginFromType<EmailPluginFake>("Email"); 

            UniversalLLMFunctionCaller planner = new(kernel);
            string ask = "What is 387 minus 22? Email the solution to John and Mary. " +
                "Tell me where the mail was sent to (the e-mail adress used) and what was wriiten in it.";

            string result = await planner.RunAsync(ask);

            Assert.IsTrue(result.Contains("@example.com") && (result.Contains("365") ));
        }
    }
}