using KleeneStar.Core;
using KleeneStar.Ldap.WebLdap;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Ldap.WebFragment
{
    /// <summary>
    /// The button of the connection page that opens the dialog adding an LDAP or LDAPS connection.
    /// </summary>
    [Section<SectionHeadlinePrimary>]
    [Scope<global::KleeneStar.Ldap.WWW.Settings.LdapConnections.Index>]
    [Cache]
    public sealed class LdapConnectionAddButtonFragment : FragmentControlButtonLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public LdapConnectionAddButtonFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = _ => "kleenestar.ldap:connection.add.label";
            Icon = _ => new IconPlus();
            Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two);
            BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Primary);
            PrimaryAction = renderContext => new ActionModal
            (
                "modal-form",
                CoreHub.GetUri<global::KleeneStar.Ldap.WWW.Settings.LdapConnections.Add>()
                    .BindParameters(renderContext.Request),
                TypeModalSize.ExtraLarge
            );
        }

        /// <summary>
        /// Renders the button for an account administrator.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The button, or <see langword="null"/>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!LdapAuthorization.MayManage(renderContext?.Request))
            {
                return null;
            }

            return base.Render(renderContext, visualTree);
        }
    }
}
