using LisBlanc.AdminPanel.Data;
using LisBlanc.AdminPanel.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LisBlanc.AdminPanel.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _context.Users.ToListAsync();
            return View(users);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, IFormCollection form)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            var newRole = form["Role"].ToString();
            var newEmail = form["Email"].ToString();

            if (string.IsNullOrEmpty(newRole)) return View(user);

            user.Role = newRole;
            user.Email = newEmail;

            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Данные пользователя успешно обновлены";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null && user.Role != "Admin")
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Пользователь удалён";
            }
            else if (user?.Role == "Admin")
            {
                TempData["Error"] = "Нельзя удалить администратора";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}

