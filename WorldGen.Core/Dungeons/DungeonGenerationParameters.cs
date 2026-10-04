using System;

namespace WorldGen.Dungeons
{
    public enum DungeonLayoutProfile { Facility, Subway, Temple, TempleReference, Raid, GroupDungeon, CaveDungeon }
    public enum DungeonVisualTheme { Industrial, Maintenance, Alien, Subway, Temple, Keep, Cavern }
    public enum DungeonAssetSource { Procedural, Rdb, Hybrid }

    public static class DungeonGenerationProfileCatalog
    {
        public const string ProfileParameter = "layoutProfile";
        public const string ThemeParameter = "visualTheme";
        public const string AssetSourceParameter = "assetSource";

        public static DungeonGenerationParameters CreateParameters(int roomCount,
            DungeonLayoutProfile profile)
        {
            switch (profile)
            {
                case DungeonLayoutProfile.Subway:
                    return new DungeonGenerationParameters(roomCount, 48000, 9000, 15000, 5000, 2600);
                case DungeonLayoutProfile.Temple:
                case DungeonLayoutProfile.TempleReference:
                    return new DungeonGenerationParameters(roomCount, 15000, 8500, 12000, 3000, 2200);
                case DungeonLayoutProfile.Raid:
                    return new DungeonGenerationParameters(roomCount, 18000, 10000, 15000, 3600, 2400);
                case DungeonLayoutProfile.GroupDungeon:
                    return new DungeonGenerationParameters(roomCount, 34000, 11000, 20000, 5500, 3000);
                case DungeonLayoutProfile.CaveDungeon:
                    return new DungeonGenerationParameters(roomCount, 38000, 14000, 24000, 8000, 4200);
                default:
                    return new DungeonGenerationParameters(roomCount);
            }
        }

        public static DungeonLayoutProfile ReadProfile(Contracts.GenerationManifest manifest)
        {
            if (manifest != null && manifest.Parameters.TryGetValue(ProfileParameter, out string value)
                && Enum.TryParse(value, true, out DungeonLayoutProfile profile)) return profile;
            return DungeonLayoutProfile.Facility;
        }

        public static DungeonVisualTheme ReadTheme(Contracts.GenerationManifest manifest)
        {
            if (manifest != null && manifest.Parameters.TryGetValue(ThemeParameter, out string value)
                && Enum.TryParse(value, true, out DungeonVisualTheme theme)) return theme;
            return DungeonVisualTheme.Industrial;
        }

        public static DungeonAssetSource ReadAssetSource(Contracts.GenerationManifest manifest)
        {
            if (manifest != null && manifest.Parameters.TryGetValue(AssetSourceParameter, out string value)
                && Enum.TryParse(value, true, out DungeonAssetSource source)) return source;
            return DungeonAssetSource.Procedural;
        }

        public static DungeonVisualTheme DefaultTheme(DungeonLayoutProfile profile)
        {
            switch (profile)
            {
                case DungeonLayoutProfile.Subway: return DungeonVisualTheme.Subway;
                case DungeonLayoutProfile.Temple: return DungeonVisualTheme.Temple;
                case DungeonLayoutProfile.TempleReference: return DungeonVisualTheme.Temple;
                case DungeonLayoutProfile.GroupDungeon: return DungeonVisualTheme.Keep;
                case DungeonLayoutProfile.CaveDungeon: return DungeonVisualTheme.Cavern;
                default: return DungeonVisualTheme.Industrial;
            }
        }
    }

    public sealed class DungeonGenerationParameters
    {
        public DungeonGenerationParameters(int roomCount, int gridSpacing = 12000,
            int minimumRoomSize = 7000, int maximumRoomSize = 10000,
            int corridorWidth = 2400, int doorwayWidth = 1800,
            int doorThickness = 300)
        {
            if (roomCount < 3 || roomCount > 256) throw new ArgumentOutOfRangeException(nameof(roomCount));
            if (gridSpacing <= 0) throw new ArgumentOutOfRangeException(nameof(gridSpacing));
            if (minimumRoomSize <= 0) throw new ArgumentOutOfRangeException(nameof(minimumRoomSize));
            if (maximumRoomSize < minimumRoomSize || maximumRoomSize >= gridSpacing)
                throw new ArgumentOutOfRangeException(nameof(maximumRoomSize));
            if (corridorWidth <= 0 || corridorWidth >= minimumRoomSize)
                throw new ArgumentOutOfRangeException(nameof(corridorWidth));
            if (doorwayWidth <= 0 || doorwayWidth > corridorWidth)
                throw new ArgumentOutOfRangeException(nameof(doorwayWidth));
            if (doorThickness <= 0 || doorThickness >= gridSpacing - maximumRoomSize)
                throw new ArgumentOutOfRangeException(nameof(doorThickness));
            RoomCount = roomCount;
            GridSpacing = gridSpacing;
            MinimumRoomSize = minimumRoomSize;
            MaximumRoomSize = maximumRoomSize;
            CorridorWidth = corridorWidth;
            DoorwayWidth = doorwayWidth;
            DoorThickness = doorThickness;
        }

        public int RoomCount { get; }
        public int GridSpacing { get; }
        public int MinimumRoomSize { get; }
        public int MaximumRoomSize { get; }
        public int CorridorWidth { get; }
        public int DoorwayWidth { get; }
        public int DoorThickness { get; }
    }
}
