using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JC.SemanticKernel.Planners.UniversalLLMFunctionCaller
{
    internal class UniversalLLMFunctionCallerInternalFunctions
    {
        [KernelFunction, Description("Call this when the workflow is done and there are no more functions to call")]
        public string  Finished(
       [Description("Wrap up what was done and what the result is, be concise")] string finalmessage
     )
        {
            return string.Empty;
        //no actual implementation, for internal routing only
        }
        [KernelFunction, Description("Gets the name of the spaceship of the user")]
        public string GetMySpaceshipName( )
        {
            return "MSS3000";
        }
        [KernelFunction, Description("Starts a Spaceship")]
        public void StartSpaceship(
       [Description("The name of the spaceship to start")] string ship_name
     )
        {
            //no actual implementation, for internal routing only
        }

    }
}
