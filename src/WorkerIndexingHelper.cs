//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Google.Protobuf.Collections;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Management.Automation.Language;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    internal class WorkerIndexingHelper
    {
        internal static IEnumerable<RpcFunctionMetadata> IndexFunctions(string baseDir)
        {
            List<string> powerShellFiles = GetPowerShellFiles(baseDir);
            //this is only necessary until we fix the worker init crap
            powerShellFiles = powerShellFiles.OrderBy(x => x.Split(Path.DirectorySeparatorChar).Count() - baseDir.Split(Path.DirectorySeparatorChar).Count() == 2 ? 0 : 1).ToList();

            List<RpcFunctionMetadata> rpcFunctionMetadatas = new List<RpcFunctionMetadata>();
            //rpcFunctionMetadatas.Add(CreateFirstFunction());
            foreach (string powerShellFile in powerShellFiles)
            {
                rpcFunctionMetadatas.AddRange(IndexFunctionsInFile(powerShellFile));
            }

            return rpcFunctionMetadatas;
        }

        private static IEnumerable<RpcFunctionMetadata> IndexFunctionsInFile(string powerShellFile)
        {
            List<RpcFunctionMetadata> fileFunctions = new List<RpcFunctionMetadata>();
            var fileAst = Parser.ParseFile(powerShellFile, out _, out ParseError[] errors);
            if (errors.Any())
            {
                throw new Exception("Couldn't parse this file");
                // TODO: Probably don't throw here?
                //return fileFunctions;
            }
            if (powerShellFile.EndsWith(".ps1")) 
            {
                // parse only the file param block, return one RpcFunctionMetadata assuming the file is the entry point
                var paramAsts = fileAst.ParamBlock;
                if (paramAsts != null && paramAsts.Attributes.Where(x => x.TypeName.ToString() == "Function").Any()) 
                {
                    // This is a function, return it 
                    fileFunctions.Add(CreateRpcMetadataFromFile(powerShellFile));
                }
            }
            else if (powerShellFile.EndsWith(".psm1"))
            {
                var potentialFunctions = fileAst.FindAll(x => x is FunctionDefinitionAst, false);
                foreach (var potentialFunction in potentialFunctions)
                {
                    var matchingBlocks = potentialFunction.FindAll(x => x is ParamBlockAst && ((ParamBlockAst)x).Attributes.Where(z => z.TypeName.ToString() == "Function").Any(), true);
                    if (matchingBlocks.Any()) {
                        //This function is one we need to register
                        fileFunctions.Add(CreateRpcMetadataFromFunctionAst(powerShellFile, (FunctionDefinitionAst)potentialFunction));
                    }
                }
                // parse all function definitions, return as many RpcFunctionMetadatas as exist in the file
            }
            return fileFunctions;
        }

        private static RpcFunctionMetadata CreateRpcMetadataFromFile(string powerShellFile)
        {
            var fileAst = Parser.ParseFile(powerShellFile, out _, out ParseError[] errors);

            RpcFunctionMetadata thisFunction = new RpcFunctionMetadata();

            thisFunction.Directory = new FileInfo(powerShellFile).Directory.FullName;
            thisFunction.ScriptFile = powerShellFile;
            thisFunction.Name = Path.GetFileName(powerShellFile).Split('.').First();
            thisFunction.Language = "powershell";

            thisFunction.FunctionId = Guid.NewGuid().ToString();
            ExtractBindings(thisFunction, fileAst.ParamBlock);

            return thisFunction;
        }
        private static RpcFunctionMetadata CreateRpcMetadataFromFunctionAst(string powerShellFile, FunctionDefinitionAst potentialFunction)
        {
            RpcFunctionMetadata thisFunction = new RpcFunctionMetadata();

            thisFunction.Directory = new FileInfo(powerShellFile).Directory.FullName;
            thisFunction.ScriptFile = powerShellFile;
            thisFunction.Name = potentialFunction.Name;
            thisFunction.EntryPoint = potentialFunction.Name;
            thisFunction.Language = "powershell";

            thisFunction.FunctionId = Guid.NewGuid().ToString();

            ParamBlockAst paramBlock = (ParamBlockAst)potentialFunction.Find(x => x is ParamBlockAst, true);
            ExtractBindings(thisFunction, paramBlock);

            return thisFunction;
        }

        private static void ExtractBindings(RpcFunctionMetadata thisFunction, ParamBlockAst paramBlock)
        {
            if (paramBlock == null)
            {
                return;
            }

            var functionAttribute = paramBlock.Attributes.Where(x => x.TypeName.Name == "Function" && x.PositionalArguments.Count > 0);
            if (functionAttribute.Any())
            {
                thisFunction.Name = functionAttribute.First().PositionalArguments[0].ToString();
            }

            List<Tuple<string, BindingInfo, string>> inputBindings = GetInputBindingInfo(paramBlock);
            foreach (Tuple<string, BindingInfo, string> inputBinding in inputBindings)
            {
                thisFunction.Bindings.Add(inputBinding.Item1, inputBinding.Item2);
                thisFunction.RawBindings.Add(inputBinding.Item3);
            }

            List<Tuple<string, BindingInfo, string>> outputBindings = GetOutputBindingInfo(paramBlock.Attributes);
            if (outputBindings.Count == 0)
            {
                outputBindings.AddRange(CreateDefaultOutputBinding(thisFunction.Bindings));
            }
            foreach (Tuple<string, BindingInfo, string> outputBinding in outputBindings)
            {
                thisFunction.Bindings.Add(outputBinding.Item1, outputBinding.Item2);
                thisFunction.RawBindings.Add(outputBinding.Item3);
            }

        }

        private static List<Tuple<string, BindingInfo, string>> GetInputBindingInfo(ParamBlockAst paramBlock)
        {
            List<Tuple<string, BindingInfo, string>> outputBindingInfo = new List<Tuple<string, BindingInfo, string>>();
            foreach (ParameterAst parameter in paramBlock.Parameters)
            {
                foreach (AttributeAst attribute in parameter.Attributes)
                {
                    string bindingName = null;
                    BindingInfo bindingInfo = new BindingInfo();
                    string rawBindingString = null;
                    switch (attribute.TypeName.ToString())
                    {
                        case "HttpTrigger":
                            bindingName = "Request";
                            //Todo: Named arguments?
                            string bindingAuthLevel = attribute.PositionalArguments.Count > 0 ? attribute.PositionalArguments[0].ToString() : "anonymous";
                            List<string> bindingMethods = attribute.PositionalArguments.Count > 1 ? ExtractOneOrMore(attribute.PositionalArguments[1]) : new List<string>() { "GET", "POST" };
                            if (bindingMethods == null)
                            {
                                bindingMethods = new List<string>() { "GET", "POST" };
                            }
                            bindingInfo.Direction = BindingInfo.Types.Direction.In;
                            bindingInfo.Type = "httpTrigger";
                            var rawHttpBinding = new
                            {
                                authLevel = bindingAuthLevel,
                                type = bindingInfo.Type,
                                direction = "in",
                                name = bindingName,
                                methods = bindingMethods
                            };
                            rawBindingString = JsonConvert.SerializeObject(rawHttpBinding);
                            outputBindingInfo.Add(new Tuple<string, BindingInfo, string>(bindingName, bindingInfo, rawBindingString));
                            break;
                        case "TimerTrigger":
                            //Todo: Named arguments?
                            bindingName = "Timer";
                            string chronExpression = attribute.PositionalArguments.Count > 0 ? attribute.PositionalArguments[0].ToString() : null;
                            bindingInfo.Direction = BindingInfo.Types.Direction.Out;
                            bindingInfo.Type = "httpTrigger";
                            var rawTimerBinding = new
    {
                                schedule = chronExpression,
                                type = bindingInfo.Type,
                                direction = "in",
                                name = bindingName,
                            };
                            rawBindingString = JsonConvert.SerializeObject(rawTimerBinding);
                            outputBindingInfo.Add(new Tuple<string, BindingInfo, string>(bindingName, bindingInfo, rawBindingString));
                            break;
                        default:
                            break;
                    }
                }
            }
            return outputBindingInfo;
        }

        private static IEnumerable<Tuple<string, BindingInfo, string>> CreateDefaultOutputBinding(MapField<string, BindingInfo> bindings)
        {
            if (bindings.Count == 0)
            {
                return new List<Tuple<string, BindingInfo, string>>();
        }
            else
            {
                var outputBindingInfo = new List<Tuple<string, BindingInfo, string>>();
                switch (bindings.ElementAt(0).Value.Type)
                {
                    case "httpTrigger":
                        outputBindingInfo.Add(CreateHttpOutputBinding());
                        break;
                    case "timerTrigger":
                        break;
                    default:
                        break;
                }
                return outputBindingInfo;
            }
        }

        private static List<Tuple<string, BindingInfo, string>> GetOutputBindingInfo(ReadOnlyCollection<AttributeAst> attributes)
        {
            List<Tuple<string, BindingInfo, string>> outputBindingInfo = new List<Tuple<string, BindingInfo, string>>();
            foreach (AttributeAst attribute in attributes)
            {
                switch(attribute.TypeName.ToString())
                {
                    case "HttpOutput":
                        string outputBindingName = attribute.PositionalArguments.Count > 0 &&
                                                   attribute.PositionalArguments[0].GetType() == typeof(StringConstantExpressionAst) ?
                                                           ((StringConstantExpressionAst)attribute.PositionalArguments[0]).Value : "Response";
                        outputBindingInfo.Add(CreateHttpOutputBinding(outputBindingName));
                        break;
                    default:
                        break;
        }
            }
            return outputBindingInfo;
        }

        private static Tuple<string, BindingInfo, string> CreateHttpOutputBinding(string name = "Response")
        {
            BindingInfo defaultOutputInfo = new BindingInfo();
            defaultOutputInfo.Type = "http";
            defaultOutputInfo.Direction = BindingInfo.Types.Direction.Out;
            return new Tuple<string, BindingInfo, string>(name, defaultOutputInfo, "{\"type\":\"http\",\"direction\":\"out\",\"name\":\"" + name + "\"}");
        }

        private static List<string> ExtractOneOrMore(ExpressionAst expressionAst)
        {
            if (expressionAst.GetType() == typeof(StringConstantExpressionAst)) 
            {
                return new List<string> { ((StringConstantExpressionAst)expressionAst).Value };
            }
            else if (expressionAst.GetType() == typeof(ArrayExpressionAst))
            {
                List<string> values = new List<string>();
                var arrayValues = ((ArrayExpressionAst)expressionAst).FindAll(x => x is StringConstantExpressionAst, false);
                foreach (StringConstantExpressionAst one in arrayValues)
        {
                    values.Add(one.Value);
                }
                return values;
            }
            return null;
        }
        private static List<string> GetPowerShellFiles(string baseDir)
        {
            if (Directory.Exists(baseDir)) 
            {
                var files = Directory.GetFiles(baseDir, "*.ps1", SearchOption.AllDirectories).ToList();
                files.AddRange(Directory.GetFiles(baseDir, "*.psm1", SearchOption.AllDirectories).ToList());
                return files;
            }
            else
            {
                throw new FileNotFoundException();
            }
        }

        private static RpcFunctionMetadata CreateSecondFunction()
        {
            RpcFunctionMetadata firstFunction = new RpcFunctionMetadata();

            BindingInfo requestInfo = new BindingInfo();
            requestInfo.Direction = BindingInfo.Types.Direction.In;
            requestInfo.Type = "httpTrigger";

            BindingInfo responseInfo = new BindingInfo();
            responseInfo.Direction = BindingInfo.Types.Direction.Out;
            responseInfo.Type = "http";

            firstFunction.Bindings.Add("Request", requestInfo);
            firstFunction.Bindings.Add("Response", responseInfo);

            firstFunction.Directory = "C:\\Users\\t-anstaples\\source\\powershell\\apat2";
            firstFunction.ScriptFile = "C:\\Users\\t-anstaples\\source\\powershell\\apat2\\new_model.psm1";
            firstFunction.EntryPoint = "ExecuteHttpTrigger";
            firstFunction.Name = "HttpTrigger2";
            firstFunction.Language = "powershell";

            firstFunction.FunctionId = Guid.NewGuid().ToString();

            firstFunction.RawBindings.Add("{\"authLevel\":\"anonymous\",\"type\":\"httpTrigger\",\"direction\":\"in\",\"name\":\"Request\",\"methods\":[\"get\",\"post\"]}");
            firstFunction.RawBindings.Add("{\"type\":\"http\",\"direction\":\"out\",\"name\":\"Response\"}");
            return firstFunction;
        }

        private static RpcFunctionMetadata CreateFirstFunction()
        {
            RpcFunctionMetadata secondFunction = new RpcFunctionMetadata();

            BindingInfo requestInfo = new BindingInfo();
            requestInfo.Direction = BindingInfo.Types.Direction.In;
            requestInfo.Type = "httpTrigger";

            BindingInfo responseInfo = new BindingInfo();
            responseInfo.Direction = BindingInfo.Types.Direction.Out;
            responseInfo.Type = "http";

            secondFunction.Bindings.Add("Request", requestInfo);
            secondFunction.Bindings.Add("Response", responseInfo);

            secondFunction.Directory = "C:\\Users\\t-anstaples\\source\\powershell\\apat2\\HttpTrigger1";
            secondFunction.ScriptFile = "C:\\Users\\t-anstaples\\source\\powershell\\apat2\\HttpTrigger1\\run.ps1";
            secondFunction.Name = "HttpTrigger1";
            secondFunction.Language = "powershell";

            secondFunction.FunctionId = Guid.NewGuid().ToString();

            secondFunction.RawBindings.Add("{\"authLevel\":\"anonymous\",\"type\":\"httpTrigger\",\"direction\":\"in\",\"name\":\"Request\",\"methods\":[\"get\",\"post\"]}");
            secondFunction.RawBindings.Add("{\"type\":\"http\",\"direction\":\"out\",\"name\":\"Response\"}");
            return secondFunction;
        }
    }
}
