using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;

namespace UWPHook
{
    /// <summary>
    /// Functions related to Windows powershell
    /// </summary>
    static class ScriptManager
    {
        public static string RunScript(string scriptText)
        {
            // create Powershell runspace
            // Scope module loading to this embedded host; do not modify the
            // user's or machine's execution-policy settings.
            var session = InitialSessionState.CreateDefault();
            session.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.RemoteSigned;
            using Runspace runspace = RunspaceFactory.CreateRunspace(session);

            // open it
            runspace.Open();

            // create a pipeline and feed it the script text
            using Pipeline pipeline = runspace.CreatePipeline();
            pipeline.Commands.AddScript(scriptText);

            // add an extra command to transform the script
            // output objects into nicely formatted strings

            // remove this line to get the actual objects
            // that the script returns. For example, the script

            // "Get-Process" returns a collection
            // of System.Diagnostics.Process instances.
            pipeline.Commands.Add("Out-String");

            // execute the script
            Collection<PSObject> results = pipeline.Invoke();

            // close the runspace
            runspace.Close();

            // convert the script result into a single string
            StringBuilder stringBuilder = new StringBuilder();
            foreach (PSObject obj in results)
            {
                stringBuilder.AppendLine(obj.ToString());
            }

            return stringBuilder.ToString();
        }
    }
}
