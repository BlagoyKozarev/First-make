using OfficeOpenXml;

namespace Core.Engine.Services;

/// <summary>
/// Helper to set EPPlus license context globally
/// </summary>
public static class ExcelHelper
{
    private static bool _licenseSet = false;

    public static void EnsureLicenseSet()
    {
        if (!_licenseSet)
        {
            // EPPlus 8: property is obsolete but still works
#pragma warning disable CS0618
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
#pragma warning restore CS0618
            _licenseSet = true;
        }
    }
}
