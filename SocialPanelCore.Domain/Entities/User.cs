using Microsoft.AspNetCore.Identity;
using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Entities;

public class User : IdentityUser<Guid>
{
    public string Name { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.UsuarioBasico;
    public DateTime CreatedAt { get; set; }

    // Navegación
    public virtual ICollection<BasePost> CreatedPosts { get; set; } = new List<BasePost>();
    public virtual ICollection<UserAccountAccess> AccountAccess { get; set; } = new List<UserAccountAccess>();
}
