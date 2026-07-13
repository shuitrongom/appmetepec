using System.Text.Json;
using appmetepec.Models;

namespace appmetepec.Services;

public sealed class PendingTicketsService
{
    private readonly string _filePath = Path.Combine(FileSystem.AppDataDirectory, "pending_tickets.json");
    private readonly string _photosDirectory = Path.Combine(FileSystem.AppDataDirectory, "pending_photos");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<List<PendingTicketSubmission>> GetAllAsync()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var items = await JsonSerializer.DeserializeAsync<List<PendingTicketSubmission>>(stream, JsonOptions);
            return items ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<string?> SavePhotoAsync(FileResult attachment)
    {
        try
        {
            Directory.CreateDirectory(_photosDirectory);
            var destination = Path.Combine(_photosDirectory, $"{Guid.NewGuid():N}{Path.GetExtension(attachment.FileName)}");
            await using var source = await attachment.OpenReadAsync();
            await using var target = File.Create(destination);
            await source.CopyToAsync(target);
            return destination;
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveAsync(PendingTicketSubmission submission)
    {
        var items = await GetAllAsync();
        items.Add(submission);
        await WriteAllAsync(items);
    }

    public async Task RemoveAsync(string id)
    {
        var items = await GetAllAsync();
        var match = items.FirstOrDefault(item => item.Id == id);
        if (match?.LocalPhotoPath is { } path && File.Exists(path))
        {
            File.Delete(path);
        }

        items.RemoveAll(item => item.Id == id);
        await WriteAllAsync(items);
    }

    private async Task WriteAllAsync(List<PendingTicketSubmission> items)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, items, JsonOptions);
    }
}
