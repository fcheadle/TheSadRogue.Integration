using System;
using System.Collections.Generic;
using System.Linq;
using GoRogue.MapGeneration;
using GoRogue.MapGeneration.ContextComponents;
using SadRogue.Primitives;
using SadRogue.Primitives.GridViews; 

namespace TheSadRogue.Integration.MapGenerationSteps
{
    /// <summary>
    /// Creates rectangles of walkable space in a brick-work pattern.
    /// </summary>
    public class BackroomGenerationStep : GenerationStep
    {
        public ItemList<Rectangle> Rooms = new ItemList<Rectangle>();
        
        protected override IEnumerator<object?> OnPerform(GenerationContext context)
        {
            Random random = new Random();
            var map = context.GetFirstOrNew<ISettableGridView<bool>>
                (()=> new ArrayView<bool>(context.Width, context.Height), "backrooms");
            Rooms = new ItemList<Rectangle>();
            var largeRooms = new List<Rectangle>();
            
            int thirdWidth = map.Width / 3;
            int halfWidth = map.Width / 2;
            int thirdHeight = map.Height / 3;
            int halfHeight = map.Height / 2; 
            
            if (random.Next(0, 2) % 2 == 0)
            {
                if (random.Next(0, 2) % 2 == 0)
                {
                    largeRooms.Add(new Rectangle(halfWidth - 1, 0, halfWidth, map.Height));
                    largeRooms.Add(new Rectangle(0, 0, halfWidth, thirdHeight));
                    largeRooms.Add(new Rectangle(0, thirdHeight - 1, halfWidth, thirdHeight));
                    largeRooms.Add(new Rectangle(0, thirdHeight * 2 - 1, halfWidth, thirdHeight));
                }
                else
                {
                    largeRooms.Add(new Rectangle(0, 0, halfWidth, map.Height));
                    largeRooms.Add(new Rectangle(halfWidth - 1, 0, halfWidth, thirdHeight));
                    largeRooms.Add(new Rectangle(halfWidth - 1, thirdHeight - 1, halfWidth, thirdHeight));
                    largeRooms.Add(new Rectangle(halfWidth - 1, thirdHeight * 2 - 1, halfWidth, thirdHeight));
                }
            }
            else
            {
                if (random.Next(0, 2) % 2 == 0)
                {
                    largeRooms.Add(new Rectangle(0,0, map.Width, halfHeight));
                    largeRooms.Add(new Rectangle(0, halfHeight - 1, thirdWidth, halfHeight));
                    largeRooms.Add(new Rectangle(thirdWidth - 1, halfHeight - 1, thirdWidth, halfHeight));
                    largeRooms.Add(new Rectangle(thirdWidth * 2 - 1, halfHeight - 1, thirdWidth, halfHeight));
                }
                else
                {
                    largeRooms.Add(new Rectangle(0,halfHeight - 1, map.Width, halfHeight));
                    largeRooms.Add(new Rectangle(0, 0, thirdWidth, halfHeight));
                    largeRooms.Add(new Rectangle(thirdWidth - 1, 0, thirdWidth, halfHeight));
                    largeRooms.Add(new Rectangle(thirdWidth * 2 - 1, 0, thirdWidth, halfHeight));
                }
            }

            foreach(var rectangle in largeRooms)
                Rooms.AddRange(rectangle.BisectRecursive(random.Next(3,9)), "backroom");

            foreach (var room in Rooms)
            {
                foreach (var point in room.Item.Positions())
                {
                    if (room.Item.PerimeterPositions().Contains(point))
                        map[point] = false;
                    else
                        map[point] = true;
                }

                yield return null;
            }
            
            yield return null;
        }
    }
}