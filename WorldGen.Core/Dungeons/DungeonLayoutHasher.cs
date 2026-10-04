using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using WorldGen.Contracts;
using WorldGen.Identity;

namespace WorldGen.Dungeons
{
    public static class DungeonLayoutHasher
    {
        public static string Compute(DungeonLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, new UTF8Encoding(false), true))
            {
                Write(writer, ManifestHasher.Compute(layout.Manifest));
                foreach (DungeonRoom room in layout.Rooms.OrderBy(r => r.Id, StringComparer.Ordinal))
                {
                    Write(writer, room.Id); writer.Write(room.Index); writer.Write((int)room.Role);
                    writer.Write((int)room.Shape);
                    writer.Write((int)room.ModuleKind);
                    writer.Write(room.Center.X); writer.Write(room.Center.Y); writer.Write(room.Center.Z);
                    writer.Write(room.Size.X); writer.Write(room.Size.Y); writer.Write(room.Size.Z);
                    foreach (DungeonCollisionBox collision in
                        DungeonCollisionBaker.BakeRoom(layout, room)
                            .OrderBy(c => c.Id, StringComparer.Ordinal))
                    {
                        Write(writer, collision.Id);
                        writer.Write((int)collision.Kind);
                        WriteBounds(writer, collision.Bounds);
                    }
                }
                foreach (DungeonConnection connection in layout.Connections.OrderBy(c => c.Id, StringComparer.Ordinal))
                {
                    Write(writer, connection.Id); Write(writer, connection.FromRoomId); Write(writer, connection.ToRoomId);
                    writer.Write(connection.IsCriticalPath);
                }
                foreach (DungeonCorridor corridor in layout.Corridors.OrderBy(c => c.Id, StringComparer.Ordinal))
                {
                    Write(writer, corridor.Id); Write(writer, corridor.ConnectionId);
                    WriteBounds(writer, corridor.WalkableBounds);
                    writer.Write(corridor.FromFloorY); writer.Write(corridor.ToFloorY);
                    foreach (DungeonCollisionBox collision in
                        DungeonCollisionBaker.BakeCorridor(layout, corridor)
                            .OrderBy(c => c.Id, StringComparer.Ordinal))
                    {
                        Write(writer, collision.Id); writer.Write((int)collision.Kind);
                        WriteBounds(writer, collision.Bounds);
                    }
                }
                foreach (DungeonDoorPortal portal in layout.DoorPortals.OrderBy(p => p.Id, StringComparer.Ordinal))
                {
                    Write(writer, portal.Id); Write(writer, portal.ConnectionId); Write(writer, portal.RoomId);
                    writer.Write((int)portal.Facing); WriteVector(writer, portal.Center);
                    WriteBounds(writer, portal.ClosedBlockingBounds);
                }
                foreach (DungeonSpawnPoint spawn in layout.SpawnPoints.OrderBy(s => s.Id, StringComparer.Ordinal))
                {
                    Write(writer, spawn.Id); Write(writer, spawn.RoomId); writer.Write((int)spawn.Role);
                    WriteVector(writer, spawn.Position); writer.Write(spawn.YawDegrees);
                }
                foreach (DungeonPresentationElement element in
                    layout.Presentation.OrderBy(e => e.Id, StringComparer.Ordinal))
                {
                    Write(writer, element.Id); Write(writer, element.OwnerId);
                    writer.Write((int)element.Kind); writer.Write((int)element.MaterialRole);
                    WriteBounds(writer, element.Bounds); writer.Write(element.BlocksMovement);
                    if (layout.Manifest.GeneratorVersion == "5.13.0")
                    { writer.Write((int)element.FixtureRole); writer.Write(element.YawDegrees); }
                }
                foreach (DungeonPrimitiveRecipe recipe in
                    layout.ConstructiveRecipes.OrderBy(r => r.Id, StringComparer.Ordinal))
                {
                    Write(writer, recipe.Id); Write(writer, recipe.OwnerId);
                    Write(writer, recipe.FixtureId); writer.Write((int)recipe.Shape);
                    WriteVector(writer, recipe.Position); WriteVector(writer, recipe.Size);
                    writer.Write(recipe.YawDegrees); writer.Write(recipe.BevelRadius);
                    writer.Write((int)recipe.MaterialRole); writer.Write(recipe.Profile.Count);
                    foreach (DungeonProfilePoint point in recipe.Profile)
                    { writer.Write(point.Radius); writer.Write(point.Height); }
                }
                writer.Flush();
                byte[] hash;
                using (SHA256 sha = SHA256.Create()) hash = sha.ComputeHash(stream.ToArray());
                return StableWorldId.Hex(hash, hash.Length);
            }
        }

        private static void WriteBounds(BinaryWriter writer, Spatial.WorldBounds bounds)
        { WriteVector(writer, bounds.Minimum); WriteVector(writer, bounds.Maximum); }

        private static void WriteVector(BinaryWriter writer, Spatial.WorldVector3 value)
        { writer.Write(value.X); writer.Write(value.Y); writer.Write(value.Z); }

        private static void Write(BinaryWriter writer, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            writer.Write(bytes.Length); writer.Write(bytes);
        }
    }
}
