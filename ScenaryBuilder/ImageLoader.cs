using System;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace EmotionAnalyzer.Components
{
    public static class ImageLoader
    {
        // Метод для загрузки изображения из ресурсов сборки
        public static BitmapImage? LoadFromResources(string resourcePath) // Добавил nullable
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                // Путь относительно компонентов
                bitmap.UriSource = new Uri($"pack://application:,,,/{GetAssemblyName()};component/{resourcePath}", UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки изображения {resourcePath}: {ex.Message}");
                return CreateFallbackImage();
            }
        }

        private static string GetAssemblyName()
        {
            return Assembly.GetExecutingAssembly().GetName().Name ?? "ScenaryBuilder";
        }

        private static BitmapImage? CreateFallbackImage() // Добавил nullable
        {
            try
            {
                // Создаем простую цветную картинку как fallback
                var fallback = new BitmapImage();
                fallback.BeginInit();
                fallback.UriSource = new Uri("data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAACXBIWXMAAAsTAAALEwEAmpwYAAAACXZwQWcAAAAgAAAAIACHaRbLAAAAGXRFWHRTb2Z0d2FyZQBBZG9iZSBJbWFnZVJlYWR5ccllPAAAAoVJREFUeNrkV01IVFEU/u6bGWfSMvwZM7HCH5wwC9NsoS3b5LJdu3Yt2rVpE9kiAqFdEUQQbSoIon+IgjYRERRUFlqkKJX2TzrqjDoz77577r2jU4U/44y+C6f3nnvu/d53zz3n3PfGhBD4n0PmP1wmk8F0Op1VFKUfgJt8dSaTSZI/FAqF9lZUVJy1VggFg0EeCASqmFWW3wcm83elxI1RVbVXVdXrJpPJQRlY8EdHR1FdXY0fcFkfSQxQcQ/sdjuy2SycTidUVUUul4PZalbg0mE1xPk9gQAAAABJRU5ErkJggg==", UriKind.Absolute);
                fallback.EndInit();
                fallback.Freeze();
                return fallback;
            }
            catch
            {
                return null;
            }
        }
    }
}