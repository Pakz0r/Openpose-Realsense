namespace Utilities.Parser
{
    using System.Threading.Tasks;
    using System.Text;
    using System.IO;

    public static class JSON
    {
        /// <summary>
        /// Froms the json.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="json">The json.</param>
        /// <returns></returns>
        public static T FromJson<T>(this string json)
        {
            return UnityEngine.JsonUtility.FromJson<T>(json);
        }

        public static object FromJson(this string json, System.Type type)
        {
            return UnityEngine.JsonUtility.FromJson(json, type);
        }

        /// <summary>
        /// Parses the asynchronous.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="json">The json.</param>
        /// <returns></returns>
        public static Task<T> FromJsonAsync<T>(this string json) where T : class
        {
            return Task.Run(() => FromJson<T>(json));
        }

        public static Task<object> FromJsonAsync(this string json, System.Type type)
        {
            return Task.Run(() => FromJson(json, type));
        }

        /// <summary>
        /// Converts an object into a JSON string.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="prettyPrint">if set to <c>true</c> [pretty print].</param>
        /// <returns></returns>
        public static string ToJson(this object obj, bool prettyPrint = false)
        {
            return UnityEngine.JsonUtility.ToJson(obj, prettyPrint);
        }

        /// <summary>
        /// Parses object from file asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path">The file path.</param>
        /// <returns></returns>
        public static Task<T> ParseFromFileAsync<T>(this string path)
        {
            return File.ReadAllTextAsync(path).ContinueWith((task) => FromJson<T>(task.Result));
        }

        public static Task<object> ParseFromFileAsync(this string path, System.Type type)
        {
            return File.ReadAllTextAsync(path).ContinueWith((task) => FromJson(task.Result, type));
        }

        /// <summary>
        /// Compose object to file asynchronously.
        /// </summary>
        /// <param name="obj">The object to compose</param>
        /// <param name="path">The file path.</param>
        /// <param name="prettyPrint">if set to <c>true</c> [pretty print].</param>
        /// <returns></returns>
        public static Task ComposeToFileAsync(object obj, string path, bool prettyPrint = false)
        {
            string json = obj.ToJson(prettyPrint);
            return File.WriteAllTextAsync(path, json, Encoding.UTF8);
        }
    }
}