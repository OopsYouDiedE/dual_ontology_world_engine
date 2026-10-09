using System.Text.Json;
namespace DualOntologyWorldEngine.Chapter2.Prototype;
/// <summary>Authored presentation text and request bindings; no fallback narrative is stored in C#.</summary>
public sealed class Chapter2InteractionContent
{
 private readonly Dictionary<string,JsonElement> _text;
 public string Text(string key) => _text.TryGetValue(key,out var value)&&value.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(value.GetString())?value.GetString()!:throw new InvalidDataException("Missing chapter UI text: "+key);
 public string[] Texts(string key,int count)
 {
  if(!_text.TryGetValue(key,out var value)||value.ValueKind!=JsonValueKind.Array)throw new InvalidDataException("Missing chapter UI text array: "+key);
  var items=value.EnumerateArray().Select(v=>v.GetString()??"").ToArray();
  if(items.Length!=count||items.Any(string.IsNullOrWhiteSpace))throw new InvalidDataException("Invalid chapter UI text array: "+key);
  return items;
 }
 public (string Label,Job Job)[] Requests { get; }
 public string Format(string key, params (string Name,string Value)[] args)
 {
  var text=Text(key);
  foreach(var (name,value) in args)text=text.Replace("{"+name+"}",value);
  return text;
 }
 public Chapter2InteractionContent(string json)
 {
  using var document=JsonDocument.Parse(json);
  var chapter=document.RootElement.GetProperty("chapters")[0];
  _text=chapter.GetProperty("ui_text").EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.Clone(),StringComparer.Ordinal);
  var ids=new HashSet<Job>();
  Requests=chapter.GetProperty("requests").EnumerateArray().Select(r=>
  {
   var label=r.GetProperty("label").GetString();
   if(string.IsNullOrWhiteSpace(label)||!Enum.TryParse<Job>(r.GetProperty("job").GetString(),out var job)||!Enum.IsDefined(job)||!ids.Add(job))throw new InvalidDataException("Invalid or duplicate chapter request.");
   return (label!,job);
  }).ToArray();
  if(Requests.Length==0)throw new InvalidDataException("Missing chapter requests.");
 }
}
