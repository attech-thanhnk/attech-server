namespace AttechServer.Shared.ApplicationBase.Common
{
    /// <summary>
    /// Enum định nghĩa các loại setting có thể có
    /// </summary>
    public enum SettingType
    {
        // Main Banners (Carousel)
        Banner1 = 1001,
        Banner2 = 1002,
        Banner3 = 1003,

        // Logo
        Logo = 2001,
        Favicon = 2002,

        // Feature Backgrounds
        HomeFeatCns = 3101,
        HomeFeatBhc = 3102,
        HomeFeatCnhk = 3103,

        // Fact Event Image
        HomeFactEvent = 3201,

        // About CNS/ATM Gallery
        AboutCns1 = 4101,
        AboutCns2 = 4102,
        AboutCns3 = 4103,
        AboutCns4 = 4104,
        AboutCns5 = 4105,
        AboutCns6 = 4106,

        // About BHC Gallery
        AboutBhc1 = 4201,
        AboutBhc2 = 4202,
        AboutBhc3 = 4203,
        AboutBhc4 = 4204,
        AboutBhc5 = 4205,

        // About CNHK Gallery
        AboutCnhk1 = 4301,
        AboutCnhk2 = 4302,
        AboutCnhk3 = 4303,
        AboutCnhk4 = 4304,
        AboutCnhk5 = 4305,
        AboutCnhk6 = 4306,
        AboutCnhk7 = 4307,
        AboutCnhk8 = 4308,

        // Home Page
        HomeHeroBackground = 5001,

        // Structure/Organization
        StructureChart = 6001,

        // Leadership
        LeaderChairman = 7001,
        LeaderDirector = 7002,
        LeaderViceDirector1 = 7003,
        LeaderViceDirector2 = 7004,
        LeaderViceDirector3 = 7005,
        LeaderViceDirector4 = 7006,

        // Future expansion
        Custom = 9999 // For dynamic setting keys
    }
    
    public static class SettingTypeExtensions
    {
        /// <summary>
        /// Chuyển setting key string thành objectId để lưu trong attachment
        /// </summary>
        public static int ToObjectId(this string settingKey)
        {
            // Nếu là enum có sẵn
            if (Enum.TryParse<SettingType>(settingKey, true, out var settingType))
            {
                return (int)settingType;
            }
            
            // Nếu là custom key, dùng hash
            return Math.Abs(settingKey.GetHashCode());
        }
        
        /// <summary>
        /// Lấy description của setting type
        /// </summary>
        public static string GetDescription(this SettingType settingType)
        {
            return settingType switch
            {
                // Main Banners (Carousel)
                SettingType.Banner1 => "Banner chính 1",
                SettingType.Banner2 => "Banner chính 2",
                SettingType.Banner3 => "Banner chính 3",

                // Logo
                SettingType.Logo => "Logo website",
                SettingType.Favicon => "Favicon",

                // Feature Backgrounds
                SettingType.HomeFeatCns => "Ảnh nền tính năng CNS",
                SettingType.HomeFeatBhc => "Ảnh nền tính năng BHC",
                SettingType.HomeFeatCnhk => "Ảnh nền tính năng CNHK",

                // Fact Event Image
                SettingType.HomeFactEvent => "Ảnh sự kiện thông tin",

                // About CNS/ATM Gallery
                SettingType.AboutCns1 => "Ảnh CNS/ATM 1",
                SettingType.AboutCns2 => "Ảnh CNS/ATM 2",
                SettingType.AboutCns3 => "Ảnh CNS/ATM 3",
                SettingType.AboutCns4 => "Ảnh DVOR DME Đà Nẵng",
                SettingType.AboutCns5 => "Ảnh DVOR DME Điện Biên",
                SettingType.AboutCns6 => "Ảnh DVOR DME Vân Đồn",

                // About BHC Gallery
                SettingType.AboutBhc1 => "Ảnh BHC 1",
                SettingType.AboutBhc2 => "Ảnh BHC 2",
                SettingType.AboutBhc3 => "Ảnh BHC 3",
                SettingType.AboutBhc4 => "Ảnh BHC 4",
                SettingType.AboutBhc5 => "Ảnh BHC 5",

                // About CNHK Gallery
                SettingType.AboutCnhk1 => "Ảnh CNHK 1",
                SettingType.AboutCnhk2 => "Ảnh CNHK 2",
                SettingType.AboutCnhk3 => "Ảnh CNHK 3",
                SettingType.AboutCnhk4 => "Ảnh CNHK 4",
                SettingType.AboutCnhk5 => "Ảnh CNHK 5",
                SettingType.AboutCnhk6 => "Ảnh CNHK 6",
                SettingType.AboutCnhk7 => "Ảnh CNHK 7",
                SettingType.AboutCnhk8 => "Ảnh CNHK 8",

                // Home Page
                SettingType.HomeHeroBackground => "Ảnh nền hero section trang chủ",

                // Structure/Organization
                SettingType.StructureChart => "Sơ đồ cơ cấu tổ chức công ty",

                // Leadership
                SettingType.LeaderChairman => "Ảnh Chủ tịch Hội đồng quản trị",
                SettingType.LeaderDirector => "Ảnh Giám đốc",
                SettingType.LeaderViceDirector1 => "Ảnh Phó giám đốc 1",
                SettingType.LeaderViceDirector2 => "Ảnh Phó giám đốc 2",
                SettingType.LeaderViceDirector3 => "Ảnh Phó giám đốc 3",
                SettingType.LeaderViceDirector4 => "Ảnh Phó giám đốc 4",

                _ => settingType.ToString()
            };
        }
    }
}