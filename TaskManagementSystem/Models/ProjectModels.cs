namespace TaskManagementSystem.Models
{
    public class Project
    {
        public int Id { get; set; } 
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime StartDate { get; set; } 
        public DateTime EndDate { get; set; } 
        public bool IsCompleted { get; set; } 
        public ICollection<Task> Tasks { get; set; } 
    }

    public class Task
    {
        public int Id { get; set; } 
        public int ProjectId { get; set; } 

        public string Name { get; set; } 
        public string Description { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsCompleted { get; set; } 

        public int Priority { get; set; }

        public int? AssignedUserId { get; set; } 

        public Project Project { get; set; } 
        public User AssignedUser { get; set; } 

        public ICollection<TaskDependency> TaskDependencies { get; set; } 
    }

    public class TaskDependency
    {
        public int Id { get; set; }

        public int TaskId { get; set; } 
        public int DependentOnTaskId { get; set; }
        public Task Task { get; set; }
        public Task DependentOnTask { get; set; } 
    }


    public class User
    {
        public int Id { get; set; } 
        public string Name { get; set; } 
        public string Email { get; set; } 
        public ICollection<Task> Tasks { get; set; }
    }


}
