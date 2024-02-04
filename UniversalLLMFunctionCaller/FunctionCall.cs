using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JC.SemanticKernel.Planners.UniversalLLMFunctionCaller
{
    internal class FunctionCall
    {
        public string Name { get; set; }
        public List<FunctionCallParameter> Parameters { get; set; }
    }
    internal class FunctionCallParameter
    {
        public string Name { get; set; }
        public  object Value { get; set; }

    }
}
