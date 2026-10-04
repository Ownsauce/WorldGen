using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WorldGen.Contracts;
using WorldGen.Spatial;

namespace WorldGen.Dungeons
{
    /// <summary>Adapts existing outputs without changing generation, RNG, saved recipes or hash formats.</summary>
    public static class DungeonGenerationResults
    {
        public const string ModularPresentationProvider = "worldgen.modular.presentation";
        public const string ModularCollisionProvider = "worldgen.modular.collision";

        public static DungeonGenerationResult GenerateModular(GenerationManifest manifest, DungeonGenerationParameters parameters)
            => FromModular(new DungeonGenerator().Generate(manifest, parameters));

        public static DungeonGenerationResult GenerateRooms(GenerationManifest manifest, NativeRoomCatalog catalog,
            int count, string pool, DungeonResourceCatalog presentation, DungeonResourceCatalog collision)
        {
            ValidateRoomManifest(manifest);
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            if (collision == null) throw new ArgumentNullException(nameof(collision));
            return FromRooms(manifest, NativeRoomDungeon.Generate(catalog, manifest.Seed, count, pool), presentation, collision);
        }

        public static DungeonGenerationResult FromModular(DungeonLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (layout.Manifest.GeneratorId != "mission-dungeon")
                throw new ArgumentException("Use the generator-specific adapter; this layout is not a modular dungeon.", nameof(layout));
            var validation = DungeonValidator.Validate(layout);
            if (!validation.IsValid) throw new ArgumentException("Invalid modular dungeon layout.", nameof(layout));
            string hash = DungeonLayoutHasher.Compute(layout);
            // These references address the complete generated room instructions, not a single GLB prefab.
            var presentation = new DungeonResourceCatalog(ModularPresentationProvider, layout.Manifest.WorldId, hash);
            var collision = new DungeonResourceCatalog(ModularCollisionProvider, layout.Manifest.WorldId, hash);
            var rooms = layout.Rooms.Select(room => new DungeonResolvedRoom(room, DungeonRoomSourceKind.GeneratedLayout,
                new DungeonSourceTransform(new WorldVector3(0, 0, 0), new WorldVector3(0, 0, 0), 0),
                presentation.Reference(room.Id), collision.Reference(room.Id))).ToList();
            var openings = new List<DungeonConnectionOpening>();
            foreach (var portal in layout.DoorPortals)
            {
                var b = portal.ClosedBlockingBounds;
                bool x = portal.Facing == DungeonPortalFacing.NegativeX || portal.Facing == DungeonPortalFacing.PositiveX;
                var outward = portal.Facing == DungeonPortalFacing.NegativeX ? new WorldVector3(-1000, 0, 0)
                    : portal.Facing == DungeonPortalFacing.PositiveX ? new WorldVector3(1000, 0, 0)
                    : portal.Facing == DungeonPortalFacing.NegativeZ ? new WorldVector3(0, 0, -1000)
                    : new WorldVector3(0, 0, 1000);
                openings.Add(new DungeonConnectionOpening(portal.Id, portal.ConnectionId, portal.RoomId, portal.Center,
                    outward, x ? b.Maximum.Z-b.Minimum.Z : b.Maximum.X-b.Minimum.X, b.Maximum.Y-b.Minimum.Y));
            }
            return new DungeonGenerationResult(layout, hash, rooms, openings, new List<DungeonPassageClosure>());
        }

        public static DungeonGenerationResult FromRooms(GenerationManifest manifest, NativeRoomRecipe recipe,
            DungeonResourceCatalog presentation, DungeonResourceCatalog collision)
        {
            ValidateRoomManifest(manifest);
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            if (collision == null) throw new ArgumentNullException(nameof(collision));
            NativeRoomDungeon.Validate(recipe.Catalog);
            var verified = NativeRoomRecipeData.Capture(recipe).Restore(recipe.Catalog);
            for (int i = 0; i < recipe.Rooms.Count; i++)
                if (recipe.Rooms[i].UsedSockets == null
                    || !recipe.Rooms[i].UsedSockets.SetEquals(verified.Rooms[i].UsedSockets))
                    throw new ArgumentException("Source-room used sockets differ from its connections.", nameof(recipe));
            recipe = verified;
            var layout = NativeRoomDungeon.Layout(manifest, recipe);
            var byIndex = layout.Rooms.ToDictionary(r => r.Index);
            var placements = recipe.Rooms.ToDictionary(r => r.Index);
            var rooms = new List<DungeonResolvedRoom>();
            foreach (var placement in recipe.Rooms)
            {
                var template = recipe.Template(placement);
                string id = placement.SourceIndex.ToString(CultureInfo.InvariantCulture);
                rooms.Add(new DungeonResolvedRoom(byIndex[placement.Index], DungeonRoomSourceKind.CatalogRoom,
                    new DungeonSourceTransform(new WorldVector3(template.OriginX, template.OriginY, template.OriginZ),
                        new WorldVector3(placement.X, placement.Y, placement.Z), placement.QuarterTurns),
                    presentation.Reference(id), collision.Reference(id)));
            }
            var openings = new List<DungeonConnectionOpening>();
            for (int i = 0; i < recipe.Joins.Count; i++)
            {
                var join = recipe.Joins[i];
                AddOpening(join.From, join.FromSocket, "join-" + i);
                AddOpening(join.To, join.ToSocket, "join-" + i);
            }
            var closures = new List<DungeonPassageClosure>();
            using (var seals = NativeRoomDungeon.Seals(recipe).GetEnumerator())
            foreach (var room in recipe.Rooms)
            for (int i = 0; i < recipe.Template(room).Sockets.Length; i++)
            {
                if (room.UsedSockets.Contains(i)) continue;
                if (!seals.MoveNext()) throw new InvalidOperationException("Missing source-room closure.");
                var seal = seals.Current;
                closures.Add(new DungeonPassageClosure("native-" + room.Index + "/socket-" + i, byIndex[room.Index].Id,
                    seal.Center, seal.Size, seal.Facing,
                    seal.Style == NativeClosureStyle.MetalPanel ? "metal-panel" : "black-backing"));
            }
            return new DungeonGenerationResult(layout, NativeRoomDungeon.Hash(manifest, recipe), rooms, openings, closures);

            void AddOpening(int index, int socketIndex, string connectionId)
            {
                var room = placements[index];
                var socket = recipe.Template(room).Sockets[socketIndex];
                int facing = (socket.Facing + room.QuarterTurns) & 3;
                var outward = facing == 0 ? new WorldVector3(0, 0, 1000)
                    : facing == 1 ? new WorldVector3(1000, 0, 0)
                    : facing == 2 ? new WorldVector3(0, 0, -1000) : new WorldVector3(-1000, 0, 0);
                openings.Add(new DungeonConnectionOpening("native-" + index + "/socket-" + socketIndex, connectionId,
                    byIndex[index].Id, NativeRoomDungeon.Transform(room, new WorldVector3(socket.X, socket.Y, socket.Z)),
                    outward, socket.Width, socket.Height));
            }
        }

        private static void ValidateRoomManifest(GenerationManifest manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (manifest.GeneratorId != NativeRoomDungeon.GeneratorId
                || manifest.GeneratorVersion != NativeRoomDungeon.GeneratorVersion || manifest.ParameterSchemaVersion != 1)
                throw new ArgumentException("Unsupported source-room generator manifest.", nameof(manifest));
        }
    }
}
