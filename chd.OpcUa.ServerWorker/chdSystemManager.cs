using chd.OpcUa.Contracts.Interfaces;
using chd.OpcUa.Server.UnderlyingSystem;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using chd.OpcUa.Server;
using chd.OpcUa.ServerWorker;
using Opc.Ua;
using Opc.Ua.Server;

namespace chd.OpcUa.ServerWorker
{
    public class chdSystemManager : UnderlyingSystemManager
    {
        protected override ValueTask<List<UnderlyingSystemSegment>> LoadSegments(CancellationToken cancellationToken)
            => ValueTask.FromResult(Build());

        private List<UnderlyingSystemSegment> Build()
        {
            var factory = CreateChild("Factory", null);
            var assets = CreateChild("Assets", null);
            var testData = CreateChild("TestData", null);

            var east = CreateChild("East", factory);
            var west = CreateChild("West", factory);

            var boiler1 = CreateChild("Boiler1", east);
            var boiler2 = CreateChild("Boiler2", west);

            boiler1.Blocks.AddRange(["Pipe1001", "Pipe1002", "Drum1002", "FC1001", "LC1001", "CC1001"]);
            boiler2.Blocks.AddRange(["Pipe2001", "Pipe2002", "Drum2002", "FC2001", "LC2001", "CC2001"]);

            east.Children.Add(boiler1);
            west.Children.Add(boiler2);
            factory.Children.AddRange([east, west]);

            var sensors = CreateChild("Sensors", assets);
            var sensorFlow = CreateChild("Flow", sensors);
            sensorFlow.Blocks.AddRange(["Pipe2001", "Pipe1002", "Pipe2001", "Pipe2002"]);

            var sensorLevel = CreateChild("Level", sensors);
            sensorLevel.Blocks.AddRange(["Drum1002", "Drum2002"]);
            sensors.Children.AddRange([sensorLevel, sensorFlow]);

            var controllers = CreateChild("Controllers", assets);
            var controllersFlow = CreateChild("Flow", sensors);
            controllersFlow.Blocks.AddRange(["FC1001", "FC2001"]);
            var controllersLevel = CreateChild("Level", sensors);
            controllersLevel.Blocks.AddRange(["LC1001", "LC2001"]);
            var controllersCustom = CreateChild("Custom", sensors);
            controllersCustom.Blocks.AddRange(["CC1001", "CC2001"]);
            controllers.Children.AddRange([controllersFlow, controllersLevel, controllersCustom]);

            assets.Children.AddRange([sensors, controllers]);

            return [factory, assets, testData];
        }

        protected override async ValueTask<UnderlyingSystemBlock> CreateBlockAsync(string blockName, CancellationToken cancellationToken)
        {
            var block = new UnderlyingSystemBlock(blockName, "", GetBockType(blockName));
            HandleBlock(block);
            return block;
        }

        private UnderlyingSystemSegment CreateChild(string name, UnderlyingSystemSegment parent)
            => new(name, parent);

        private string GetBockType(string blockName) => blockName switch
        {
            var x when x.StartsWith("Pipe") => "FlowSensor",
            var x when x.StartsWith("Drum") => "LevelSensor",
            var x when x.StartsWith("FC") || x.StartsWith("LC") => "Controller",
            var x when x.StartsWith("CC") => "CustomController",
            _ => "Unknown"
        };


        private void HandleBlock(UnderlyingSystemBlock block)
        {
            switch (block.BlockType)
            {
                case "FlowSensor":
                    {
                        block.CreateTag<float>("Measurement", "liters/sec", false);
                        //block.CreateTag<ESystemState>("Status", "", false, Enum.GetNames<ESystemState>());
                        break;
                    }

                case "LevelSensor":
                    {
                        block.CreateTag<float>("Measurement", "liters", false);
                        //block.CreateTag<ESystemState>("Status", "", false, Enum.GetNames<ESystemState>());
                        break;
                    }

                case "Controller":
                    {
                        block.CreateTag<int>("SetPoint", "", true);
                        block.CreateTag<float>("Measurement", "liters", false);
                        block.CreateTag<int>("Output", "", false);
                        //block.CreateTag<ESystemState>("Status", "", false, Enum.GetNames<ESystemState>());
                        break;
                    }

                case "CustomController":
                    {
                        block.CreateTag<bool>("Input1", "", true);
                        block.CreateTag<int>("Input2", "", true);
                        block.CreateTag<decimal>("Input3", "", true);
                        block.CreateTag<string>("Input4", "", true);
                        //block.CreateTag<ESystemState>("Status", "", false, Enum.GetNames<ESystemState>());

                        block.AddEvent("Test", "Test", typeof(string));

                        block.AddMethod(nameof(StartCustomController), "", true, (method) =>
                        {
                            method.CreateInputArgument("Initial State", typeof(uint));
                            method.CreateInputArgument("Final State", typeof(uint));

                            method.CreateOutputArgument("Initial State", typeof(uint));
                            method.CreateOutputArgument("Final State", typeof(uint));
                        });
                        //block.AddMethod(nameof(Sum), HandleSystemMethod);
                        break;
                    }
            }
        }


        public async ValueTask<object[]> StartCustomController(uint initalState, uint finalState, CancellationToken cancellationToken)
        {
            var b = await this.FindBlockByIdentifier("CC1001", cancellationToken);
            _ = await b.WriteTagValueAsync("Input2", (int)initalState + (int)finalState, cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

            await b.TriggerEvent("Test", "Did it", EventSeverity.Medium, cancellationToken);

            return new object[] { finalState, initalState };
        }
    }
}
