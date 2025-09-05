using System;
using System.IO;

namespace EvoDesigner
{
    public class TestBase64
    {
        public static void TestImageConversion()
        {
            // Create a simple test image file (1x1 PNG)
            byte[] pngBytes = {
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
                0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
                0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0xD7, 0x63, 0xF8, 0x0F, 0x00, 0x00,
                0x01, 0x00, 0x01, 0x14, 0x6D, 0xD0, 0x9C, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
                0x42, 0x60, 0x82
            };
            
            string testImagePath = "test_image.png";
            File.WriteAllBytes(testImagePath, pngBytes);
            
            // Convert to base64
            string base64 = Convert.ToBase64String(pngBytes);
            Console.WriteLine($"Base64 length: {base64.Length}");
            Console.WriteLine($"Base64 data: {base64.Substring(0, Math.Min(50, base64.Length))}...");
            
            // Verify round-trip conversion
            byte[] convertedBack = Convert.FromBase64String(base64);
            bool isIdentical = true;
            if (convertedBack.Length == pngBytes.Length)
            {
                for (int i = 0; i < pngBytes.Length; i++)
                {
                    if (pngBytes[i] != convertedBack[i])
                    {
                        isIdentical = false;
                        break;
                    }
                }
            }
            else
            {
                isIdentical = false;
            }
            
            Console.WriteLine($"Round-trip conversion successful: {isIdentical}");
            
            // Clean up
            if (File.Exists(testImagePath))
                File.Delete(testImagePath);
        }
    }
}
