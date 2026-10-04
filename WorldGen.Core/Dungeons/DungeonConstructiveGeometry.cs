using System;
using System.Collections.Generic;
using WorldGen.Spatial;

namespace WorldGen.Dungeons
{
    public enum DungeonConstructiveShape
    {
        RoundedBox = 0, Cylinder = 1, RoundedRing = 2, LathedProfile = 3, Basin = 4
    }

    public readonly struct DungeonProfilePoint
    {
        public DungeonProfilePoint(int radius, int height) { Radius = radius; Height = height; }
        public int Radius { get; }
        public int Height { get; }
    }

    public sealed class DungeonPrimitiveRecipe
    {
        public DungeonPrimitiveRecipe(string id, string ownerId, string fixtureId,
            DungeonConstructiveShape shape, WorldVector3 position, WorldVector3 size,
            int yawDegrees, int bevelRadius, DungeonMaterialRole materialRole,
            IReadOnlyList<DungeonProfilePoint> profile = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            OwnerId = ownerId ?? throw new ArgumentNullException(nameof(ownerId));
            FixtureId = fixtureId ?? throw new ArgumentNullException(nameof(fixtureId));
            Shape = shape; Position = position; Size = size; YawDegrees = yawDegrees;
            BevelRadius = bevelRadius; MaterialRole = materialRole;
            Profile = profile ?? Array.Empty<DungeonProfilePoint>();
        }
        public string Id { get; }
        public string OwnerId { get; }
        public string FixtureId { get; }
        public DungeonConstructiveShape Shape { get; }
        public WorldVector3 Position { get; }
        public WorldVector3 Size { get; }
        public int YawDegrees { get; }
        public int BevelRadius { get; }
        public DungeonMaterialRole MaterialRole { get; }
        public IReadOnlyList<DungeonProfilePoint> Profile { get; }
    }

    public static class DungeonConstructiveRecipeBuilder
    {
        public static IReadOnlyList<DungeonPrimitiveRecipe> Build(DungeonLayout layout)
        {
            var recipes = new List<DungeonPrimitiveRecipe>();
            foreach (DungeonRoom room in layout.Rooms)
            {
                if (room.ModuleKind != DungeonModuleKind.Restroom) continue;
                foreach (DungeonPresentationElement element in layout.Presentation)
                {
                    if (element.OwnerId != room.Id) continue;
                    if (element.FixtureRole == DungeonFixtureRole.Toilet || element.FixtureRole == DungeonFixtureRole.Sink) continue;
                    if (element.Id.Contains("/toilet-")) AddToilet(element, recipes);
                    else if (element.Id.Contains("/sink-")) AddSink(element, recipes);
                }
            }
            return recipes.AsReadOnly();
        }

        private static void AddToilet(DungeonPresentationElement fixture,
            List<DungeonPrimitiveRecipe> recipes)
        {
            WorldBounds b = fixture.Bounds;
            int cx = (b.Minimum.X + b.Maximum.X) / 2;
            int cz = (b.Minimum.Z + b.Maximum.Z) / 2;
            int sx = b.Maximum.X - b.Minimum.X;
            int sz = b.Maximum.Z - b.Minimum.Z;
            int yaw = sx > sz ? 90 : 0;
            string key = fixture.Id;
            recipes.Add(new DungeonPrimitiveRecipe(key + "/bowl", fixture.OwnerId, key,
                DungeonConstructiveShape.LathedProfile,
                new WorldVector3(cx, b.Minimum.Y + 260, cz),
                new WorldVector3(sx, 440, sz), yaw, 45,
                DungeonMaterialRole.Porcelain,
                new[] { new DungeonProfilePoint(270, -220),
                    new DungeonProfilePoint(390, -80), new DungeonProfilePoint(430, 80),
                    new DungeonProfilePoint(360, 220) }));
            recipes.Add(new DungeonPrimitiveRecipe(key + "/seat", fixture.OwnerId, key,
                DungeonConstructiveShape.RoundedRing,
                new WorldVector3(cx, b.Minimum.Y + 500, cz),
                new WorldVector3(sx - 40, 55, sz - 80), yaw, 70,
                DungeonMaterialRole.DarkStructuralMetal));
            recipes.Add(new DungeonPrimitiveRecipe(key + "/housing", fixture.OwnerId, key,
                DungeonConstructiveShape.RoundedBox,
                new WorldVector3(cx, b.Minimum.Y + 470,
                    cz + (yaw == 0 ? sz / 3 : 0)),
                new WorldVector3(sx - 100, 520, Math.Max(180, sz / 3)), yaw, 70,
                DungeonMaterialRole.Porcelain));
        }

        private static void AddSink(DungeonPresentationElement fixture,
            List<DungeonPrimitiveRecipe> recipes)
        {
            WorldBounds b = fixture.Bounds;
            int cx = (b.Minimum.X + b.Maximum.X) / 2;
            int cy = (b.Minimum.Y + b.Maximum.Y) / 2;
            int cz = (b.Minimum.Z + b.Maximum.Z) / 2;
            int sx = b.Maximum.X - b.Minimum.X, sz = b.Maximum.Z - b.Minimum.Z;
            int yaw = sx < sz ? 90 : 0;
            string key = fixture.Id;
            recipes.Add(new DungeonPrimitiveRecipe(key + "/basin", fixture.OwnerId, key,
                DungeonConstructiveShape.Basin, new WorldVector3(cx, cy, cz),
                new WorldVector3(sx, 220, sz), yaw, 90, DungeonMaterialRole.Porcelain));
            recipes.Add(new DungeonPrimitiveRecipe(key + "/faucet", fixture.OwnerId, key,
                DungeonConstructiveShape.Cylinder,
                new WorldVector3(cx, b.Maximum.Y + 150, cz),
                new WorldVector3(55, 300, 55), yaw, 0,
                DungeonMaterialRole.ModernPanelMetal));
        }
    }
}
