using System.Text.Json;
using System.Text.Json.Serialization;
using ConsoleApp3;

namespace ConsoleApp2
{
    
    public class TodoTask
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = "";
        public DateTime? DueDate { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Priority Priority { get; set; } = Priority.Medium;
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Completion IsCompleted { get; set; } = Completion.Completed;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public override string ToString()
        {
            var due = DueDate.HasValue ? DueDate.Value.ToString("yyyy-MM-dd") : "(no due date)";
            return $"[{Id}] {(IsCompleted == Completion.Completed ? "✔" : " ")} {Title} ({Priority}) - Due: {due}\n    {Description}";
        }
    }

    public class TaskRepository
    {
        private readonly string _filePath;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        public TaskRepository(string? filePath = null)
        {
            _filePath = filePath ?? Path.Combine(AppContext.BaseDirectory, "tasks.json");
        }

        public async Task<List<TodoTask>> LoadAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                if (!File.Exists(_filePath))
                {
                    return new List<TodoTask>();
                }

                await using var fs = File.OpenRead(_filePath);
                var list = await JsonSerializer.DeserializeAsync<List<TodoTask>>(fs, _jsonOptions) 
                           ?? new List<TodoTask>();
                return list;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task SaveAsync(List<TodoTask> tasks)
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

        public async Task<TodoTask> AddAsync(TodoTask task)
        {
            var tasks = await LoadAsync();
            task.Id = tasks.Any() ? tasks.Max(t => t.Id) + 1 : 1;
            tasks.Add(task);
            await SaveAsync(tasks);
            return task;
        }

        public async Task<bool> UpdateAsync(TodoTask task)
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

        public async Task<TodoTask?> GetByIdAsync(int id)
        {
            var tasks = await LoadAsync();
            return tasks.FirstOrDefault(t => t.Id == id);
        }
    }

    public class TaskManager
    {
        private readonly TaskRepository _repository;
        // InputCollector inputCollector = new InputCollector();
        InputCollector inputCollector = new InputCollector();
        public TaskManager(TaskRepository repo)
        {
            _repository = repo;
        }

        public async Task RunAsync()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== Task Manager (JSON-backed) ===\n");
                Console.WriteLine("Input integer number to perform operation!\n");
                Console.WriteLine("1) Add Task");
                Console.WriteLine("2) Update Task");
                Console.WriteLine("3) Delete Task");
                Console.WriteLine("4) Filter Tasks");
                Console.WriteLine("5) Summary");
                Console.WriteLine("0) Exit\n");
                Console.Write("Choose an option: ");
                var choice = Console.ReadLine()?.Trim();

                switch (choice)
                {
                    case "1": await AddTaskAsync(); break;
                    case "2": await UpdateTaskAsync(); break;
                    case "3": await DeleteTaskAsync(); break;
                    case "4": await ViewTasksAsync(); break;
                    case "5": await ShowSummaryAsync(); break;
                    case "0": return;
                    default:
                        Console.WriteLine("Invalid choice. Press any key to continue...");
                        Console.ReadKey();
                        break;
                }
            }
        }

        private async Task AddTaskAsync()
        {
            Console.Clear();
            Console.WriteLine("-- Add Task --\n");
            var title = inputCollector.PromptRequired("Title: ");
            var desc = inputCollector.PromptRequired("Description: ");
            var due = inputCollector.PromptDate("Due date (yyyy-MM-dd) or leave empty: ");
            var pr = inputCollector.PromptPriority();
            var cmp = inputCollector.PromptCompletion();

            var task = new TodoTask
            {
                Title = title,
                Description = desc,
                DueDate = due,
                Priority = pr,
                IsCompleted = cmp,
                CreatedAt = DateTime.UtcNow
            };

            var added = await _repository.AddAsync(task);
            Console.WriteLine($"\nTask added with ID {added.Id}. Press any key to continue...");
            Console.ReadKey();
        }

        private async Task UpdateTaskAsync()
        {
            Console.Clear();
            Console.WriteLine("-- Update Task --\n");
            var id = inputCollector.PromptInt("Enter task ID to update: ");
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
            {
                Console.WriteLine("Task not found. Press any key to continue...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("Current:\n" + existing + "\n");
            Console.WriteLine("Leave empty to keep current value.");

            var title = inputCollector.PromptNullable("New Title: ");
            var desc = inputCollector.PromptNullable("New Description: ");
            var due = inputCollector.PromptDateNullable("New Due date (yyyy-MM-dd) or leave empty: ");
            var pr = inputCollector.PromptPriorityNullable(existing.Priority);
            var completed = inputCollector.PromptCompletionNullable(existing.IsCompleted);

            if (!string.IsNullOrWhiteSpace(title)) existing.Title = title;
            if (desc != null) existing.Description = desc;
            if (due.HasValue) existing.DueDate = due;
            if (pr.HasValue) existing.Priority = pr.Value;
            if (completed.HasValue) existing.IsCompleted = completed.Value;

            var ok = await _repository.UpdateAsync(existing);
            Console.WriteLine(ok ? "Task updated. Press any key..." : "Failed to update. Press any key...");
            Console.ReadKey();
        }

        private async Task DeleteTaskAsync()
        {
            Console.Clear();
            Console.WriteLine("-- Delete Task --\n");
            var id = inputCollector.PromptInt("Enter task ID to delete: ");
            var existing = await _repository.GetByIdAsync(id);
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
                var ok = await _repository.DeleteAsync(id);
                Console.WriteLine(ok ? "Deleted. Press any key..." : "Failed to delete. Press any key...");
            }
            else
            {
                Console.WriteLine("Cancelled. Press any key...");
            }
            Console.ReadKey();
        }

        private async Task ViewTasksAsync()
        {
            Console.Clear();
            Console.WriteLine("-- View / Filter Tasks --\n");
            Console.WriteLine("1) All tasks");
            Console.WriteLine("2) Filter by completion status");
            Console.WriteLine("3) Filter by priority");
            Console.WriteLine("4) Due date range (e.g., due this week)");
            Console.WriteLine("0) Back\n");
            Console.Write("Choice: ");
            var choice = Console.ReadLine()?.Trim();

            var tasks = await _repository.LoadAsync();
            IEnumerable<TodoTask> result =
                tasks.OrderBy(t => t.DueDate ?? DateTime.MaxValue).ThenByDescending(t => t.Priority);

            switch (choice)
            {
                case "1": break; // keep all
                case "2":
                    Console.WriteLine("a) Completed\nb) Pending");
                    var s = inputCollector.Prompt("Choice: ");
                    if (s == "a")
                        result = result.Where(t =>
                            t.IsCompleted == Completion.Completed || t.IsCompleted == Completion.Pending);
                    else result = result.Where(t => !(t.IsCompleted == Completion.Completed || t.IsCompleted == Completion.Pending));
                    break;
                case "3":
                    var p = inputCollector.PromptPriority();
                    result = result.Where(t => t.Priority == p);
                    break;
                case "4":
                    Console.WriteLine("a) Due this week\nb) Custom range");
                    var r = inputCollector.Prompt("Choice: ");
                    if (r == "a")
                    {
                        var today = DateTime.Today;
                        var weekEnd = today.AddDays(7);
                        result = result.Where(t =>
                            t.DueDate.HasValue && t.DueDate.Value.Date >= today && t.DueDate.Value.Date <= weekEnd);
                    }
                    else
                    {
                        var from = inputCollector.PromptDate("From (yyyy-MM-dd): ");
                        var to = inputCollector.PromptDate("To (yyyy-MM-dd): ");
                        result = result.Where(t =>
                            t.DueDate.HasValue && t.DueDate.Value.Date >= from.Date && t.DueDate.Value.Date <= to.Date);
                    }

                    break;
                default:
                    Console.WriteLine("Invalid choice. Press any key...");
                    Console.ReadKey();
                    return;
            }

            Console.Clear();
            Console.WriteLine($"-- Tasks ({result.Count()}) --\n");
            foreach (var t in result)
            {
                Console.WriteLine(t + "\n");
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
            
        }

        private async Task ShowSummaryAsync()
        {
            Console.Clear();
            Console.WriteLine("-- Summary --\n");
            var tasks = await _repository.LoadAsync();
            var byPriority = tasks.GroupBy(t => t.Priority).Select(g => (g.Key, Count: g.Count())).OrderByDescending(x => x.Key);
            var byCompleted = tasks.GroupBy(t => t.IsCompleted).Select(g => (g.Key, Count: g.Count())).OrderByDescending(x => x.Key);

            foreach (var (priority, count) in byPriority)
            {
                Console.WriteLine($"{count} {priority} Priority");
            }
            foreach (var (completion, count) in byCompleted)
            {
                if (completion == Completion.Completed)
                    Console.WriteLine($"{count} {completion}");
                if(completion == Completion.Pending)
                    Console.WriteLine($"{count} {completion}");
            }
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
        
    }
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.Write("Try the appliaction --> (1) Todo App (2) Library App: ");
            int choice = Convert.ToInt16(Console.ReadLine());

            switch (choice)
            {
                case 1:
                    var repo = new TaskRepository();
                    var manager = new TaskManager(repo);
    
                    Console.WriteLine("Welcome to Todo Console App (JSON storage)");
                    Console.WriteLine("Data file: " + Path.Combine(AppContext.BaseDirectory, "tasks.json"));
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey();
    
                    await manager.RunAsync();
    
                    Console.WriteLine("Goodbye!");
                    break;
                case 2:
                    var database = new LibraryDatatabase();
                    var Librarian = new LibraryManager(database);
            
                    Console.WriteLine("Welcome to Library App(JSON Storage)");
                    Console.WriteLine("Data file: " + Path.Combine(AppContext.BaseDirectory, "tasks.json"));
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey();
            
                    await Librarian.RunAsync();
                    Console.WriteLine("Goodbye!");
                    break;
                default:
                    Console.WriteLine("Invalid, exit.");
                    break;
            }
            
        }
    }
}
