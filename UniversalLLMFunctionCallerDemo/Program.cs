using JC.SemanticKernel.Planners.UniversalLLMFunctionCaller;
using Microsoft.SemanticKernel;
using SemanticKernel.IntegrationTests.Fakes;
using UniversalLLMFunctionCallerDemo.Plugins;

namespace UniversalLLMFunctionCallerDemo
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            IKernelBuilder builder = Kernel.CreateBuilder();
            builder.AddMistralChatCompletion(Environment.GetEnvironmentVariable("mistral_key"), "mistral-medium");
            var kernel = builder.Build();
            kernel.ImportPluginFromType<TimePlugin>("Time");
            kernel.ImportPluginFromType<MathPlugin>("Math");
            kernel.ImportPluginFromType<EmailPluginFake>("Email");

            UniversalLLMFunctionCaller planner = new(kernel);
            string ask = "What is the current hour number, plus 5?";
            Console.WriteLine(ask);
            string result = await     planner.RunAsync("What is the current hour number, plus 5?");
            Console.WriteLine(result);
        }
    }
}
