using System.Text.Json;
using System.Text.Json.Serialization;
using ConsoleApp3;

namespace ConsoleApp2
{
    
    public class LibData
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public int PublicationYear { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ReadStatus IsRead { get; set; } = ReadStatus.Read;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class LibraryDatatabase
    {
        private readonly string _filePath;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        public LibraryDatatabase(string? filepath = null)
        {
            _filePath = filepath ?? Path.Combine(AppContext.BaseDirectory, "Libtask.json");
        }
        
        public async Task<List<LibData>> LoadAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                if (!File.Exists(_filePath))
                {
                    return new List<LibData>();
                }

                await using var fs = File.OpenRead(_filePath);
                var list = await JsonSerializer.DeserializeAsync<List<LibData>>(fs, _jsonOptions) 
                           ?? new List<LibData>();
                return list;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        public async Task SaveAsync(List<LibData> tasks)
        {
            await _semaphore.WaitAsync();
            try
            {
                var temp = _filePath + ".tmp";
                await using (var fileStream =
                             new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    await JsonSerializer.SerializeAsync(fileStream, tasks, _jsonOptions);
                    await fileStream.FlushAsync();
                }

                File.Copy(temp, _filePath, true);
                File.Delete(temp);
            }
            catch (Exception exception) when (exception is JsonException || exception is IOException)
            {
                Console.WriteLine($"JSON Error: {exception.Message}");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<LibData> AddAsync(LibData task)
        {
            var tasks = await LoadAsync();
            task.Id = tasks.Any() ? tasks.Max(t => t.Id) + 1 : 1;
            tasks.Add(task);
            await SaveAsync(tasks);
            return task;
        }

        public async Task<bool> UpdateAsync(LibData task)
        {
            var tasks = await LoadAsync();
            var idx = tasks.FindIndex(t => t.Id == task.Id);
            if (idx == -1) return false;
            tasks[idx] = task;
            await SaveAsync(tasks);
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var tasks = await LoadAsync();
            var removed = tasks.RemoveAll(t => t.Id == id) > 0;
            if (!removed) return false;
            await SaveAsync(tasks);
            return true;
        }

        public async Task<LibData?> GetByIdAsync(int id)
        {
            var tasks = await LoadAsync();
            return tasks.FirstOrDefault(t => t.Id == id);
        }
    }

    public class LibraryManager
    {
        private readonly LibraryDatatabase _database;
        InputCollector inputCollector = new InputCollector();
        public LibraryManager(LibraryDatatabase database)
        {
            _database = database;
        }

        public async Task RunAsync()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== Task Manager (JSON-backed) ===\n");
                Console.WriteLine("Input integer number to perform operation!\n");
                Console.WriteLine("1) Add Book");
                Console.WriteLine("2) Update Book"); // update book status by ID
                Console.WriteLine("3) Remove(Delete) Book");// delete by ID
                Console.WriteLine("4) search books: ");//search by Genre or Author
                Console.WriteLine("5) Summary");//Total number of books, unread and read books, most common genre using LINQ
                Console.WriteLine("0) Exit\n");
                Console.Write("Choose an option: ");
                var choice = Console.ReadLine()?.Trim();

                switch (choice)
                {
                    case "1": await AddBookAsync(); break;
                    case "2": await UpdateBookAsync(); break;
                    case "3": await DeleteBookAsync(); break;
                    case "4": await SearchBookAsync(); break;
                    case "5": await ShowBookSummaryAsync(); break;
                    case "0": return;
                    default:
                        Console.WriteLine("Invalid choice. Press any key to continue...");
                        Console.ReadKey();
                        break;
                }
            }
        }

        private async Task AddBookAsync()
        {
            Console.Clear();
            Console.WriteLine("-- Add Task --\n");
            var title = inputCollector.PromptRequired("Title: ");
            var author = inputCollector.PromptRequired("Author: ");
            var genre = inputCollector.PromptRequired("Genre: ");
            var publishYear = inputCollector.PromptIntRead("Publication Year: ");
            var read = inputCollector.PromptReadStatus();

            var task = new LibData()
            {
                Title = title,
                Author = author,
                Genre = genre,
                PublicationYear = publishYear,
                IsRead = read,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var added = await _database.AddAsync(task);
            Console.WriteLine($"\nTask added with ID {added.Id}. Press any key to continue...");
            Console.ReadKey();
        }

        private async Task UpdateBookAsync()
        {
            Console.Clear();
            Console.WriteLine("-- Update Book --\n");
            var id = inputCollector.PromptInt("Enter task ID to update: ");
            var existing = await _database.GetByIdAsync(id);
            if (existing == null)
            {
                Console.WriteLine("Book not found. Press any key to continue...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("Current:\n" + existing + "\n");
            Console.WriteLine("Leave empty to keep current value.");

            var title = inputCollector.PromptNullable("New Title: ");
            var author = inputCollector.PromptNullable("New Author: ");
            var genre = inputCollector.PromptNullable("Ne or leave empty: ");
            var publishYear = inputCollector.PromptIntReadNullable("YYYY(e.g 2001) or leave empty: ");
            var readStatus = inputCollector.PromptReadStatusNullable(existing.IsRead);

            if (!string.IsNullOrWhiteSpace(title)) existing.Title = title;
            if (author != null) existing.Author = author;
            if (genre != null) existing.Genre = genre;
            if (publishYear.HasValue) existing.PublicationYear = publishYear.Value;
            if (readStatus.HasValue) existing.IsRead = readStatus.Value;

            var ok = await _database.UpdateAsync(existing);
            Console.WriteLine(ok ? "Task updated. Press any key..." : "Failed to update. Press any key...");
            Console.ReadKey();
        }

        private async Task DeleteBookAsync()
        {
            Console.Clear();
            Console.WriteLine("-- Delete Book --\n");
            var id = inputCollector.PromptInt("Enter task ID to delete: ");
            var existing = await _database.GetByIdAsync(id);
            if (existing == null)
            {
                Console.WriteLine("Task not found. Press any key to continue...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("About to delete:\n" + existing + "\n");
            var confirm = inputCollector.Prompt("Type DELETE to confirm: ");
            if (confirm == "DELETE")
            {
                var ok = await _database.DeleteAsync(id);
                Console.WriteLine(ok ? "Deleted. Press any key..." : "Failed to delete. Press any key...");
            }
            else
            {
                Console.WriteLine("Cancelled. Press any key...");
            }
            Console.ReadKey();
        }

        private async Task SearchBookAsync()
        {
            Console.Clear();
            Console.WriteLine("--- Search For Books ---");
            Console.WriteLine("1) By Genre");
            Console.WriteLine("2) By Author");
            Console.WriteLine("3) By ReadStatus");
            Console.WriteLine("4) By Publication Year");
            Console.WriteLine("0) Back\n");
            Console.Write("Choice: ");
            var choice = Console.ReadLine()?.Trim();

            var books = await _database.LoadAsync();
            IEnumerable<LibData> result =
                books.OrderBy(t => t.Id).ThenByDescending(t => t.PublicationYear);

            switch (choice)
            {
                case "1":
                    Console.WriteLine("Search by Genre: ");
                    var searchGenre = inputCollector.Prompt("Genre Choice: ");
                    result = books.Where(b =>
                        string.IsNullOrEmpty(searchGenre) ||
                        b.Genre.Equals(searchGenre, StringComparison.OrdinalIgnoreCase));
                    break;
                case "2":
                    Console.WriteLine("Search by Author: ");
                    var searchAuthor = inputCollector.Prompt("Author Choice: ");
                    result = books.Where(b =>
                        string.IsNullOrEmpty(searchAuthor) ||
                        b.Author.Equals(searchAuthor, StringComparison.OrdinalIgnoreCase));
                    break;
                case "3":
                    Console.WriteLine("Search by ReadStatus: ");
                    var searchReadStatus = inputCollector.PromptReadStatus();
                    result = books.Where(b => b.IsRead == searchReadStatus);
                    break;
                case "4":
                    Console.WriteLine("Search by Publication");
                    var pstart = inputCollector.PromptIntReadNullable("Search startDate: ");
                    var pend = inputCollector.PromptIntReadNullable("Search EndDate: ");
                    result = books.Where(b => (!pstart.HasValue || b.PublicationYear >= pstart.Value) &&
                                              (!pend.HasValue || b.PublicationYear <= pend.Value));
                    break;
                default:
                    Console.WriteLine("Invalid choice. Press any key...");
                    Console.ReadKey();
                    return;
            }
            
            Console.Clear();
            Console.WriteLine($"-- Books ({result.Count()}) --\n");
            foreach (var Ebook in result)
            {
                Console.WriteLine(Ebook + "\n");
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        private async Task ShowBookSummaryAsync()
        {
            Console.Clear();
            Console.WriteLine("-- Summary --\n");
            var tasks = await _database.LoadAsync();
            var byReadStatus = tasks.GroupBy(t => t.IsRead).Select(g => (g.Key, Count: g.Count())).OrderByDescending(x => x.Key);
            var books = await _database.LoadAsync();
            IEnumerable<LibData> TotalBooks =
                books.OrderBy(t => t.Id).ThenByDescending(t => t.PublicationYear);
            
            Console.WriteLine($"Total number of books: {TotalBooks}");
            foreach (var (readStat,count) in byReadStatus)
            {
                if (readStat == ReadStatus.Read)
                    Console.WriteLine($"{count} {readStat}");
                if(readStat == ReadStatus.Unread)
                    Console.WriteLine($"{count} {readStat}");
            }

            var McommonGenre = tasks.GroupBy(g => g.Genre).OrderByDescending(t => t.Count()).FirstOrDefault()?.Key;
            Console.WriteLine($"Most Common Genre: {McommonGenre}");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
    }
}

