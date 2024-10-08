﻿using System.Reflection;
using AzureProjectTestLib.Helper;
using NUnit.Common;
using NUnitLite;

namespace AzureProjectTest;

internal class Run
{
    private static int Main(string[] args)
    {
        // var trace = "trace";
        // var tempDir = @"/workspaces/AzureAutomaticGradingEngine_Assignments/testing";
        // var tempCredentialsFilePath = @"/workspaces/AzureAutomaticGradingEngine_Assignments/testing/sp.json";
        // var where = "";
        // var where = "test==\"AzureProjectTestLib.VnetTests.Test01_Have2VnetsIn2Regions\"||test==\"AzureProjectTestLib.VnetTests.Test02_VnetAddressSpace\"";
        // var where = "test==\"AzureProjectTestLib.AppServiceTest.Test04_FunctionAppSettings\"";


        var tempCredentialsFilePath = args[0];
        var tempDir = args[1];
        var trace = args[2];
        var where = args[3];
        Console.WriteLine("tempCredentialsFilePath:" + tempCredentialsFilePath);
        Console.WriteLine("trace:" + trace);
        Console.WriteLine("where:" + where);


        var strWriter = new StringWriter();
        var autoRun = new AutoRun(typeof(Config).GetTypeInfo().Assembly);

        var runTestParameters = new List<string>
        {
            "/test:AzureProjectTestLib",
            "--work=" + tempDir,
            "--output=" + tempDir,
            "--err=" + tempDir,
            "--params:AzureCredentialsPath=" + tempCredentialsFilePath + ";trace=" + trace
        };
        if (!string.IsNullOrEmpty(where)) runTestParameters.Insert(1, "--where=" + where );
        Console.WriteLine(runTestParameters.ToArray());
        var returnCode = autoRun.Execute(runTestParameters.ToArray(), new ExtendedTextWrapper(strWriter), Console.In);

        Console.WriteLine(strWriter.ToString());

        //var xml = File.ReadAllText(Path.Combine(tempDir, "TestResult.xml"));
        //Console.WriteLine(returnCode);
        //Console.WriteLine(xml);
        return returnCode;
    }
}