namespace WorldGen.Dungeons
{
    /// <summary>Source metres to placed metres without quantizing collision vertices.</summary>
    public static class NativeRoomTransform
    {
        public static void SourcePoint(NativeRoomTemplate template, NativeRoomPlacement placement,
            float x, float y, float z, out float worldX, out float worldY, out float worldZ)
        {
            var transform = new DungeonSourceTransform(
                new WorldGen.Spatial.WorldVector3(template.OriginX, template.OriginY, template.OriginZ),
                new WorldGen.Spatial.WorldVector3(placement.X, placement.Y, placement.Z),
                placement.QuarterTurns & 3);
            transform.TransformPoint(x, y, z, out worldX, out worldY, out worldZ);
        }
    }
}
