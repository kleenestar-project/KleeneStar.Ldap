using KleeneStar.Ldap.WebLdap;
using KleeneStar.Ldap.WebParameter;
using System;
using System.Collections.Generic;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Ldap.WebFragment
{
    /// <summary>
    /// A dialog form that runs an action on one connection - test it, synchronize it: a short
    /// explanation, what is known about the connection, and a button that posts the
    /// connection's id to the action's endpoint. The endpoint's answer is shown in the dialog.
    /// </summary>
    public abstract class LdapConnectionActionFormFragment : FragmentControlDataForm
    {
        /// <summary>
        /// Gets the button that runs the action.
        /// </summary>
        public ControlFormItemButtonSubmit Submit { get; }

        /// <summary>
        /// Gets the mode the rest form loads in; edit, so it loads with the connection's id.
        /// </summary>
        public override Func<IRenderControlFormContext, string> Mode => _ => TypeRestFormMode.Edit.ToMode();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        /// <param name="submit">The button.</param>
        protected LdapConnectionActionFormFragment(IFragmentContext fragmentContext, ControlFormItemButtonSubmit submit)
            : base(fragmentContext)
        {
            Submit = submit;
            Method = _ => RequestMethod.POST;
            ItemId = renderContext => renderContext.Request.GetParameter<LdapConnectionIdParameter>()?.Value;

            AddPrimaryButton(Submit);
        }

        /// <summary>
        /// Gets the key of the explanation shown above the button.
        /// </summary>
        protected abstract string Explanation { get; }

        /// <summary>
        /// Returns the lines describing the connection.
        /// </summary>
        /// <param name="settings">The connection.</param>
        /// <param name="request">The request.</param>
        /// <returns>The lines.</returns>
        protected virtual IEnumerable<string> Describe(LdapSourceSettings settings, IRequest request)
        {
            yield return $"{settings.Name} ({settings.Key}) · {settings.Server}:{settings.EffectivePort} · "
                + I18N.Translate(request, $"kleenestar.ldap:connection.protocol.{settings.Protocol.ToString().ToLowerInvariant()}");
        }

        /// <summary>
        /// Renders the form with the explanation and the description of the connection.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The form, or <see langword="null"/> for a caller who may not manage connections.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var request = renderContext?.Request;

            if (!LdapAuthorization.MayManage(request))
            {
                return null;
            }

            var id = Guid.TryParse(request.GetParameter<LdapConnectionIdParameter>()?.Value, out var parsed) ? parsed : Guid.Empty;
            var settings = LdapHub.GetEffective(id);
            var items = new List<ControlFormItem>
            {
                new ControlFormItemStaticText { Text = _ => Explanation }
            };

            if (settings is null)
            {
                items.Add(new ControlFormItemStaticText { Text = _ => "kleenestar.ldap:connection.unknown" });
            }
            else
            {
                foreach (var line in Describe(settings, request))
                {
                    items.Add(new ControlFormItemStaticText { Text = _ => line });
                }
            }

            return base.Render(renderContext, visualTree, items);
        }
    }
}
