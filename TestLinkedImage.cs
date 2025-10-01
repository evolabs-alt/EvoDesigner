using System;
using System.IO;
using System.Windows.Controls;
using EvoDesigner.UI;

namespace EvoDesigner
{
    public class TestLinkedImage
    {
        public static void TestCreateLinkedImageElement()
        {
            try
            {
                File.AppendAllText("test-linkedimage.log", $"{DateTime.Now}: Starting LinkedImage test\n");
                
                // Create instance of MainWindow to access CreateElementByType method
                var mainWindow = new MainWindow();
                
                // Test calling CreateElementByType directly
                var elementType = "LinkedImage";
                File.AppendAllText("test-linkedimage.log", $"{DateTime.Now}: Calling CreateElementByType with '{elementType}'\n");
                
                // Use reflection to call the private method
                var methodInfo = typeof(MainWindow).GetMethod("CreateElementByType", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (methodInfo != null)
                {
                    var result = methodInfo.Invoke(mainWindow, new object[] { elementType });
                    File.AppendAllText("test-linkedimage.log", $"{DateTime.Now}: CreateElementByType returned: {result?.GetType().Name ?? "NULL"}\n");
                    
                    if (result is Image image)
                    {
                        File.AppendAllText("test-linkedimage.log", $"{DateTime.Now}: SUCCESS - Image element created with Tag: {image.Tag}\n");
                    }
                    else
                    {
                        File.AppendAllText("test-linkedimage.log", $"{DateTime.Now}: ERROR - Expected Image but got: {result?.GetType().Name ?? "NULL"}\n");
                    }
                }
                else
                {
                    File.AppendAllText("test-linkedimage.log", $"{DateTime.Now}: ERROR - Could not find CreateElementByType method via reflection\n");
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText("test-linkedimage.log", $"{DateTime.Now}: EXCEPTION - {ex.Message}\n");
                File.AppendAllText("test-linkedimage.log", $"{DateTime.Now}: STACK TRACE - {ex.StackTrace}\n");
            }
        }
    }
}