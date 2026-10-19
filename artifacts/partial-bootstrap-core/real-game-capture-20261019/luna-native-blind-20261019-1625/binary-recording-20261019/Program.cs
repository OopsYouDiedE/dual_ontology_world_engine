using DuelView.Compiled;
using System.Text.Json;
using System.Security.Cryptography;
var directory = args[0];
Directory.CreateDirectory(directory);
var exact = Rational.From("2/3");
var child = R.snapshot(new List<object> { "shared", exact, "2/3", -0.0, new List<object> { true, false, "中文\n\u0000" } });
var value = R.snapshot(new List<object> { new List<object> { "values", Enumerable.Repeat(child, 1000).ToList() }, new List<object> { "empty", new List<object>() } });
var path = Path.Combine(directory, "value.dvsave");
R.WriteEvidenceFile(path, value);
var restored = R.ReadEvidenceFile(path);
if (!(bool)R.same_value(value, restored)) throw new Exception("Binary value differs from original");
var entries = R.Items(R.record_find(restored, "values", false));
if (!ReferenceEquals(entries[0], entries[999])) throw new Exception("Shared immutable node was expanded");
if (R.Items(entries[0])[1] is not Rational || R.Items(entries[0])[2] is not string) throw new Exception("Exact number/string types lost");
if (BitConverter.DoubleToInt64Bits((double)R.Items(entries[0])[3]) != long.MinValue) throw new Exception("Negative zero lost");
var hash = SHA256.HashData(File.ReadAllBytes(path));
try { R.WriteEvidenceFile(path, value); throw new Exception("Overwrite accepted"); } catch (IOException) { }
if (!hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path)))) throw new Exception("Existing evidence changed");
try { R.WriteEvidenceFile(Path.Combine(directory, "forbidden.json"), value); throw new Exception("JSON path accepted"); } catch (IOException) { }
var damaged = File.ReadAllBytes(path); damaged[^1] ^= 1;
File.WriteAllBytes(Path.Combine(directory, "corrupt.dvsave"), damaged);
try { R.ReadEvidenceFile(Path.Combine(directory, "corrupt.dvsave")); throw new Exception("Corruption accepted"); } catch (InvalidDataException) { }
File.WriteAllBytes(Path.Combine(directory, "truncated.dvsave"), File.ReadAllBytes(path)[..^5]);
try { R.ReadEvidenceFile(Path.Combine(directory, "truncated.dvsave")); throw new Exception("Truncation accepted"); } catch (EndOfStreamException) { }
BinaryEvidence.WriteJsonRecords(Path.Combine(directory, "records.dvlog"), new[] { "{\"number\":42,\"text\":\"中文\",\"flag\":true}", "{\"number\":-0.0}" });
Console.WriteLine("Original value, exact atoms, negative zero and shared references survived binary restore; overwrite, JSON path, corruption and truncation were rejected. No game session opened.");
