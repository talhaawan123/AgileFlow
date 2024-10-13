using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskManagementSystem.Models;

namespace TaskManagementSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TaskController : ControllerBase
    {
        private readonly TaskManagementContext _context;

        public TaskController(TaskManagementContext context)
        {
            _context = context;
        }

        // GET: api/Task
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TaskManagementSystem.Models.Task>>> GetTasks()
        {
            return await _context.Tasks.Include(t => t.TaskDependencies).ToListAsync();
        }

        // GET: api/Task/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TaskManagementSystem.Models.Task>> GetTask(int id)
        {
            var task = await _context.Tasks
                .Include(t => t.TaskDependencies)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
            {
                return NotFound();
            }

            return task;
        }

        // POST: api/Task
        [HttpPost]
        public async Task<ActionResult<TaskManagementSystem.Models.Task>> PostTask(TaskManagementSystem.Models.Task task)
        {
            var project = await _context.Projects.FindAsync(task.ProjectId);
            if (project == null)
            {
                return NotFound("Project not found.");
            }

            // Calculate EndDate based on duration and adjust it for working days
            task.EndDate = AdjustEndDate(task.StartDate, (task.EndDate - task.StartDate).Days);

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTask), new { id = task.Id }, task);
        }

        // PUT: api/Task/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTask(int id, TaskManagementSystem.Models.Task task)
        {
            if (id != task.Id)
            {
                return BadRequest();
            }

            _context.Entry(task).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();

                // Adjust dependent tasks and mark project completed if applicable
                if (task.IsCompleted)
                {
                    await AdjustDependentTasksAsync(task.Id);
                    await CheckAndMarkProjectCompletedAsync(task.ProjectId);
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TaskExists(id))
                {
                    return NotFound();
                }
                throw;
            }

            return NoContent();
        }

        // DELETE: api/Task/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTask(int id)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task == null)
            {
                return NotFound();
            }

            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: api/Task/AddDependency
        [HttpPost("AddDependency")]
        public async Task<ActionResult> AddDependency(int taskId, int dependentOnTaskId)
        {
            var task = await _context.Tasks.FindAsync(taskId);
            var dependentTask = await _context.Tasks.FindAsync(dependentOnTaskId);

            if (task == null || dependentTask == null)
            {
                return NotFound("One or both tasks not found.");
            }

            // Check for cyclic dependencies
            if (HasCyclicDependency(taskId, dependentOnTaskId))
            {
                return BadRequest("Cyclic dependency detected.");
            }

            var dependency = new TaskDependency
            {
                TaskId = taskId,
                DependentOnTaskId = dependentOnTaskId
            };

            _context.TaskDependencies.Add(dependency);
            await _context.SaveChangesAsync();

            return Ok();
        }

        // DELETE: api/Task/RemoveDependency
        [HttpDelete("RemoveDependency")]
        public async Task<ActionResult> RemoveDependency(int taskId, int dependentOnTaskId)
        {
            var dependency = await _context.TaskDependencies
                .FirstOrDefaultAsync(td => td.TaskId == taskId && td.DependentOnTaskId == dependentOnTaskId);

            if (dependency == null)
            {
                return NotFound("Dependency not found.");
            }

            _context.TaskDependencies.Remove(dependency);
            await _context.SaveChangesAsync();

            return Ok();
        }

        // GET tasks assigned to a specific user
        [HttpGet("User/{userId}/Tasks")]
        public async Task<ActionResult<IEnumerable<TaskManagementSystem.Models.Task>>> GetTasksByUser(int userId)
        {
            var user = await _context.Users.Include(u => u.Tasks).FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return NotFound();
            }

            return Ok(user.Tasks);
        }

        // Assign a task to a user
        [HttpPut("AssignTask/{taskId}/User/{userId}")]
        public async Task<IActionResult> AssignTaskToUser(int taskId, int userId)
        {
            var task = await _context.Tasks.FindAsync(taskId);
            var user = await _context.Users.FindAsync(userId);

            if (task == null || user == null)
            {
                return NotFound("Task or User not found.");
            }

            task.AssignedUserId = userId;
            task.AssignedUser = user;

            await _context.SaveChangesAsync();

            return Ok(task);
        }

        // Adjust EndDate based on working days
        private DateTime AdjustEndDate(DateTime startDate, int durationInDays)
        {
            DateTime endDate = startDate;
            int addedDays = 0;

            while (addedDays < durationInDays)
            {
                endDate = endDate.AddDays(1);
                if (endDate.DayOfWeek != DayOfWeek.Saturday && endDate.DayOfWeek != DayOfWeek.Sunday)
                {
                    addedDays++;
                }
            }

            return endDate;
        }

        // Check if task exists
        private bool TaskExists(int id)
        {
            return _context.Tasks.Any(e => e.Id == id);
        }

        // Adjust dependent task start dates based on the completion of the current task
        [HttpPut("AdjustDependentTasks/{taskId}")]
        public async Task<IActionResult> AdjustDependentTasksAsync(int taskId)
        {
            var task = await _context.Tasks.Include(t => t.TaskDependencies).FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null)
            {
                return NotFound("Task not found.");
            }

            foreach (var dependency in task.TaskDependencies)
            {
                var dependentTask = await _context.Tasks.FindAsync(dependency.DependentOnTaskId);
                if (dependentTask != null)
                {
                    dependentTask.StartDate = task.EndDate.AddDays(1); // Start after the main task
                    dependentTask.EndDate = AdjustEndDate(dependentTask.StartDate, (dependentTask.EndDate - dependentTask.StartDate).Days);
                }
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // Check if all tasks in a project are completed
        [HttpPut("CheckAndMarkProjectCompleted/{projectId}")]
        public async Task<IActionResult> CheckAndMarkProjectCompletedAsync(int projectId)
        {
            var project = await _context.Projects
                .Include(p => p.Tasks) // Ensure to include tasks
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
            {
                return NotFound("Project not found.");
            }

            // Check if all tasks are completed
            bool allTasksCompleted = project.Tasks.All(t => t.IsCompleted);

            if (allTasksCompleted)
            {
                project.IsCompleted = true; // Assuming you have an IsCompleted property on your project model
                await _context.SaveChangesAsync();
                return Ok("Project marked as completed.");
            }

            // If not all tasks are completed, return an appropriate response
            return Ok("Project is not yet completed.");
        }


        // Check for circular dependencies
        private bool HasCyclicDependency(int taskId, int dependentOnTaskId)
        {
            if (taskId == dependentOnTaskId)
            {
                return true; // A task cannot depend on itself
            }

            var dependentTasks = _context.TaskDependencies.Where(td => td.TaskId == dependentOnTaskId).Select(td => td.DependentOnTaskId).ToList();
            foreach (var depTaskId in dependentTasks)
            {
                if (HasCyclicDependency(taskId, depTaskId))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
