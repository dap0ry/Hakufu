using System.Text.Json.Serialization;

namespace Hakufu.MVVM.Model;

public class CatalogVolume
{
    [JsonPropertyName("label")]       public string Label       { get; set; } = "";
    [JsonPropertyName("downloadUrl")] public string DownloadUrl { get; set; } = "";
}

public class CatalogItem
{
    [JsonPropertyName("id")]          public string      Id          { get; set; } = "";
    [JsonPropertyName("title")]       public string      Title       { get; set; } = "";
    [JsonPropertyName("author")]      public string      Author      { get; set; } = "";
    [JsonPropertyName("description")] public string      Description { get; set; } = "";
    [JsonPropertyName("coverUrl")]    public string      CoverUrl    { get; set; } = "";
    [JsonPropertyName("pages")]       public int         Pages       { get; set; }
    [JsonPropertyName("sizeMb")]      public double      SizeMb      { get; set; }
    [JsonPropertyName("tags")]        public List<string>      Tags    { get; set; } = [];
    [JsonPropertyName("volumes")]     public List<CatalogVolume> Volumes { get; set; } = [];
}

public class MangaCatalog
{
    [JsonPropertyName("version")] public int               Version { get; set; } = 1;
    [JsonPropertyName("items")]   public List<CatalogItem> Items   { get; set; } = [];
}
