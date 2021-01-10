using System;
using System.Collections.Generic;
using System.Linq;
using GoRogue.MapGeneration;
using GoRogue.MapGeneration.Steps;
using SadRogue.Primitives;
using SadRogue.Primitives.GridViews;

namespace TheSadRogue.Integration.MapGenerationSteps
{
    /// <summary>
    /// This generation step sections the map into regions and then applies a different generation step to each region.
    /// </summary>
    public class CompositeGenerationStep : GenerationStep
    {
        private readonly Dictionary<string, GenerationStep[]> _stepSets;
        public CompositeGenerationStep()
        {
            _stepSets = new Dictionary<string, GenerationStep[]>()
            {
                { "maze", new GenerationStep[] { new MazeGeneration("maze", wallFloorComponentTag:"maze", tunnelsComponentTag:"tunnel") } },
                { "backrooms", new GenerationStep[] { new BackroomGenerationStep() } },
                { "parallelograms", new GenerationStep[] { new ParallelogramGenerationStep() } },
                { "spiral", new GenerationStep[] { new SpiralGenerationStep() } },
                { 
                    "cave", new GenerationStep[]
                    {
                        new RandomViewFill("cave", "cave"),
                        new CellularAutomataAreaGeneration("cave", "cave"),
                    }
                },
                //{ "connector", new GenerationStep[] { new ClosestMapAreaConnection("connector", "composite", "areas", "compositetunnel") } },
            };
        }
        
        protected override IEnumerator<object?> OnPerform(GenerationContext context)
        {
            Random random = new Random();
            var map = context.GetFirstOrNew<ISettableGridView<bool>>
                (() => new ArrayView<bool>(context.Width, context.Height), "composite");
            
            var generator = new Generator(map.Width, map.Height);
            
            foreach(var set in _stepSets)
                generator.AddSteps(set.Value);

            generator = generator.Generate();

            var spiral = generator.Context.GetFirst<ISettableGridView<bool>>("spiral");
            var backrooms = generator.Context.GetFirst<ISettableGridView<bool>>("backrooms");
            var parallelograms = generator.Context.GetFirst<ISettableGridView<bool>>("parallelograms");
            var caves = generator.Context.GetFirst<ISettableGridView<bool>>("cave");
            var maze = generator.Context.GetFirst<ISettableGridView<bool>>("maze");
            //var connections = generator.Context.GetFirst<ISettableGridView<bool>>(new ClosestMapAreaConnection().TunnelsComponentTag);
            
            foreach (var region in GenerateRegions(map.Width, map.Height, 45, 8))
            {
                int chance = random.Next(0, 4);
                foreach (var here in region.Points)
                {
                    if (map.Contains(here))
                    {
                        switch (chance)
                        {
                            case 0:
                                map[here] = spiral[here];
                                break;
                            case 1: 
                                map[here] = backrooms[here];
                                break;
                            case 2: 
                                map[here] = parallelograms[here];
                                break;
                            case 3: 
                                map[here] = maze[here];
                                break;
                            case 4:
                                map[here] = caves[here];
                                break;
                        }
                    }
                }

                yield return null;
            }
        }
        
        /// <summary>
        /// Sections the map into regions of equal size
        /// </summary>
        /// <returns></returns>
        private IEnumerable<Region> GenerateRegions(int mapWidth, int mapHeight, double rotationAngle, int minimumDimension)
        {
            var wholeMap = new Rectangle(-mapWidth, -mapHeight,mapWidth * 3,mapHeight * 3);
            var center = (mapWidth / 2, mapHeight / 2);
            var regions = wholeMap.BisectRecursive(minimumDimension);
            foreach (var room in regions)
            {
                Region region = Region.FromRectangle("room", room).Rotate(rotationAngle, center);
                if (region.Points.Any(p => p.X >= 0 && p.X < mapWidth && p.Y >= 0 && p.Y < mapHeight))
                    yield return region;
            }
        }
    }
}