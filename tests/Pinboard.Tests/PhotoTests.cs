using System.IO;
using Pinboard;
using Xunit;

namespace Pinboard.Tests;

public class PhotoTests
{
    [Fact]
    public void Shuffle_shows_every_photo_before_repeating()
    {
        var shuffle = new PhotoShuffle(new[] { "a", "b", "c" }, new Random(1));
        var firstRound = new[] { shuffle.Next(), shuffle.Next(), shuffle.Next() };
        Assert.Equal(new[] { "a", "b", "c" }, firstRound.OrderBy(x => x));
    }

    [Fact]
    public void Shuffle_does_not_repeat_across_rounds()
    {
        var shuffle = new PhotoShuffle(new[] { "a", "b" }, new Random(3));
        string? previous = shuffle.Next();
        for (int i = 0; i < 20; i++)
        {
            string? next = shuffle.Next();
            Assert.NotEqual(previous, next);
            previous = next;
        }
    }

    [Fact]
    public void Shuffle_of_nothing_is_null() => Assert.Null(new PhotoShuffle(Array.Empty<string>(), new Random()).Next());

    [Fact]
    public void Missing_folder_has_no_photos() =>
        Assert.Empty(PhotoFiles.Find(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));

    [Fact]
    public void Photos_are_found_in_subfolders_and_other_files_ignored()
    {
        string dir = Directory.CreateTempSubdirectory("pinboard-photos").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.jpg"), "");
            File.WriteAllText(Path.Combine(dir, "b.txt"), "");
            Directory.CreateDirectory(Path.Combine(dir, "sub"));
            File.WriteAllText(Path.Combine(dir, "sub", "c.PNG"), "");
            Assert.Equal(2, PhotoFiles.Find(dir).Count);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Broken_image_loads_as_null()
    {
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".jpg");
        File.WriteAllText(file, "not an image");
        try
        {
            Assert.Null(PhotoFiles.TryLoad(file, 400));
        }
        finally
        {
            File.Delete(file);
        }
    }
}
