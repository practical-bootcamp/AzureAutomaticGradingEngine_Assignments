﻿using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Azure;
using Azure.AI.OpenAI;
using AzureProjectTestLib.Helper;

namespace GraderFunctionApp
{
    public static class GameTaskFunction
    {

        private static async Task<string> Rephrases(string sentence)
        {
            var openAiClient = new OpenAIClient(
                new Uri(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!),
                new AzureKeyCredential(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")!)
            );
            var chatCompletionsOptions = new ChatCompletionsOptions
            {
                Messages =
                {
                    new ChatMessage(ChatRole.System, "You are a Microsoft Azure game dialogue designer,Good at designing lively and interesting dialogue." +
                                                     "You only reply instruction to ask the player setup something in Microsoft Azure."),
                    new ChatMessage(ChatRole.User,
                        $"You need to help me rewrite a sentence with the following rule:" +
                        $"1. Keep all technical teams and Noun. " +
                        $"2. It is instructions to ask player to complete tasks." +
                        $"3. In a funny style to the brave (勇者) with some emojis" +
                        $"4. In both English and Traditional Chinese version." +
                        $"5. English version goes first, and Chinese version foes next." +
                        $"6. Only reply the rewritten sentence, and don't answer anything else." +
                        $"Rewrite the following sentence:\n\n\n{sentence}\n"
                        ),
                },
                Temperature = (float)0.9,
                MaxTokens = 800,
                NucleusSamplingFactor = (float)0.95,
                FrequencyPenalty = 0,
                PresencePenalty = 0,
            };
            var chatCompletionsResponse = await openAiClient.GetChatCompletionsAsync(
                Environment.GetEnvironmentVariable("DEPLOYMENT_OR_MODEL_NAME")!,
                chatCompletionsOptions
            );

            var chatMessage = chatCompletionsResponse.Value.Choices[0].Message;
            return chatMessage.Content;
        }

        [FunctionName("GameTaskFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            var assembly = Assembly.GetAssembly(type: typeof(GameClassAttribute));
            var allTasks = new List<Task<GameTaskData>>();
            foreach (var testClass in GetTypesWithHelpAttribute(assembly))
            {
                var gameClass = testClass.GetCustomAttribute<GameClassAttribute>();
                var tasks = testClass.GetMethods().Where(m => m.GetCustomAttribute<GameTaskAttribute>() != null)
                    .Select(c => new { c.Name, GameTask = c.GetCustomAttribute<GameTaskAttribute>()! });

                var independentTests = tasks.Where(c => c.GameTask.GroupNumber == -1)
                    .Select(async c => new GameTaskData()
                    {
                        Name = testClass.FullName + "." + c.Name,
                        Tests = new[] { testClass.FullName + "." + c.Name },
                        GameClassOrder = gameClass!.Order,
                        Instruction = await Rephrases(c.GameTask.Instruction),
                        Filter = "test=" + testClass.FullName + "." + c.Name,
                        Reward = c.GameTask.Reward,
                        TimeLimit = c.GameTask.TimeLimit
                    });


                var groupedTasks = tasks.Where(c => c.GameTask.GroupNumber != -1)
                    .GroupBy(c => c.GameTask.GroupNumber)
                    .Select(async c =>
                        new GameTaskData()
                        {
                            Name = string.Join(" ", c.Select(a => testClass.FullName + "." + a.Name)),
                            Tests = c.Select(a => testClass.FullName + "." + a.Name).ToArray(),
                            GameClassOrder = gameClass!.Order,
                            Instruction = await Rephrases(string.Join("", c.Select(a => a.GameTask.Instruction))),
                            Filter = string.Join("||", c.Select(a => "test==\"" + testClass.FullName + "." + a.Name + "\"")),
                            Reward = c.Sum(a => a.GameTask.Reward),
                            TimeLimit = c.Sum(a => a.GameTask.TimeLimit),
                        }
                    );

                allTasks.AddRange(independentTests);
                allTasks.AddRange(groupedTasks);
            }

            var allCompletedTask = allTasks.Select(t => t.Result).ToList();
            var serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
            allCompletedTask = allCompletedTask.OrderBy(c => c.GameClassOrder).ThenBy(c => c.Tests.First()).ToList();
            var json = JsonConvert.SerializeObject(allCompletedTask.ToArray(), serializerSettings);

            return new ContentResult { Content = json, ContentType = "application/json", StatusCode = 200 };

            static IEnumerable<Type> GetTypesWithHelpAttribute(Assembly assembly)
            {
                return from Type type in assembly!.GetTypes()
                       where type.GetCustomAttributes(typeof(GameClassAttribute), true).Length > 0
                       select type;
            }
        }
    }
}
