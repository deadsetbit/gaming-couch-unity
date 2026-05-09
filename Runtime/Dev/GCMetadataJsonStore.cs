#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DSB.GC.Dev
{
    internal sealed class GCMetadataJsonStore
    {
        private readonly IGCLocalProjectRootResolver projectRootResolver;

        internal GCMetadataJsonStore()
            : this(new GCUnityLocalProjectRootResolver())
        {
        }

        internal GCMetadataJsonStore(IGCLocalProjectRootResolver projectRootResolver)
        {
            if (projectRootResolver == null)
            {
                throw new ArgumentNullException(nameof(projectRootResolver));
            }

            this.projectRootResolver = projectRootResolver;
        }

        internal string ResolveFilePath()
        {
            return Path.Combine(projectRootResolver.ResolveProjectRootPath(), GCMetadataJsonFile.FileName);
        }

        internal GCRootJsonFileStamp ReadFileStamp()
        {
            return GCRootJsonFileStamp.Read(ResolveFilePath());
        }

        internal GCMetadataJsonReadResult Read()
        {
            return GCMetadataJsonValidation.BuildReadResult(ReadParsedFile(ResolveFilePath()));
        }

        private static GCMetadataJsonParsedFile ReadParsedFile(string path)
        {
            if (!File.Exists(path))
            {
                return GCMetadataJsonParsedFile.Missing(path);
            }

            try
            {
                var token = JToken.Parse(File.ReadAllText(path, Encoding.UTF8));
                var jsonObject = token as JObject;
                if (jsonObject == null)
                {
                    return GCMetadataJsonParsedFile.InvalidRoot(path);
                }

                return GCMetadataJsonParsedFile.Parsed(path, jsonObject);
            }
            catch (JsonException exception)
            {
                return GCMetadataJsonParsedFile.InvalidJson(path, "gc.metadata.json is not valid JSON: " + exception.Message);
            }
            catch (Exception exception)
            {
                return GCMetadataJsonParsedFile.ReadError(path, "gc.metadata.json could not be read: " + exception.Message);
            }
        }
    }
}
#endif
