using System;
using System.Threading.Tasks;
using MetaverseCloudEngine.Common.Models.Forms;
using MetaverseCloudEngine.Unity.Async;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    public class MetaverseAccountEditor : EditorWindow
    {
        private enum LoginPage
        {
            LogIn,
            Register,
            ResetPassword
        }

        private static LoginPage _page;
        private static string _errorMessage;
        private static string _successMessage;
        private static string _username;
        private static string _email;
        private static string _password;
        private static bool _rememberMe = true;
        private static string _confirmPassword;
        private static bool _makingRequest;
        private Vector2 _scroll;

        [MenuItem(MetaverseConstants.MenuItems.WindowsMenuRootPath + "Account")]
        public static void Open()
        {
            _page = LoginPage.LogIn;
            _password = string.Empty;
            var window = GetWindow<MetaverseAccountEditor>();
            window.titleContent = new GUIContent("Account", MetaverseEditorUtils.EditorIcon);
            window.maxSize = window.minSize = new Vector2(400, 300);
            window.ShowUtility();
        }

        public static void LoginRequired(string messageOverride = null)
        {
            EditorUtility.DisplayDialog("Please Log In", !string.IsNullOrEmpty(messageOverride) ? messageOverride : "Please log in to continue.", "Ok");
            Open();
        }

        public static void LoginButton(string messageOverride = null)
        {
            EditorGUILayout.HelpBox(!string.IsNullOrEmpty(messageOverride) ? messageOverride : "Please log in.", MessageType.Info);
            if (GUILayout.Button("Log In"))
            {
                Open();
            }
        }

        private void OnDisable()
        {
            _password = string.Empty;
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            var isLoading = !MetaverseProgram.Initialized || _makingRequest;
            MetaverseEditorUtils.DrawLoadingScreen(Draw, MetaverseEditorUtils.DrawDefaultLoadingScreen, isLoading, true);

            EditorGUILayout.EndScrollView();
        }

        private void Update()
        {
            Repaint();
        }

        private static void Draw()
        {
            if (MetaverseProgram.ApiClient == null)
                return;

            if (!MetaverseProgram.ApiClient.Account.IsLoggedIn)
            {
                DrawLogin();
            }
            else
            {
                DrawLoggedIn();
            }
        }

        private static void DrawLoggedIn()
        {
            var currentUser = MetaverseProgram.ApiClient.Account.CurrentUser;
            MetaverseEditorUtils.Header("Hi there, " + currentUser.UserName + "!");

            if (GUILayout.Button("Manage Assets", EditorStyles.toolbarButton, GUILayout.Height(25)))
            {
                AssetManagerEditorWindow.Open();
                GUIUtility.ExitGUI();
                return;
            }

            MetaverseEditorUtils.Box(() =>
            {
                MetaverseEditorUtils.Box(() =>
                {
                    EditorGUILayout.LabelField("Username", EditorStyles.boldLabel, GUILayout.Width(70));
                    EditorGUILayout.LabelField(currentUser.UserName);

                }, vertical: false);
                MetaverseEditorUtils.Box(() =>
                {
                    EditorGUILayout.LabelField("Email", EditorStyles.boldLabel, GUILayout.Width(70));
                    EditorGUILayout.LabelField(currentUser.Email);

                }, vertical: false);
            });

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Log Out", EditorStyles.toolbarButton) && EditorUtility.DisplayDialog("Log Out", "Are you sure you want to log out of your account?", "Yes", "Nevermind"))
            {
                Task.Run(async () => await MetaverseProgram.ApiClient.Account.LogOutAsync()).Wait();
            }
        }

        private static void DrawLogin()
        {
            MetaverseEditorUtils.Header(
                _page switch
                {
                    LoginPage.LogIn => "Please Log In",
                    LoginPage.Register => "Register Account",
                    LoginPage.ResetPassword => "Reset Password",
                    _ => "Error"
                });

            MetaverseEditorUtils.Error(_errorMessage);

            GUILayout.FlexibleSpace();

            MetaverseEditorUtils.Box(() =>
            {
                if (_page is not LoginPage.ResetPassword)
                    _username = MetaverseEditorUtils.TextField("Username", _username);
                if (_page is LoginPage.Register or LoginPage.ResetPassword)
                    _email = MetaverseEditorUtils.TextField("Email", _email);
                if (_page is LoginPage.LogIn or LoginPage.Register)
                {
                    _password = MetaverseEditorUtils.TextField("Password", _password, true);
                    if (_page is LoginPage.LogIn)
                        _rememberMe = EditorGUILayout.Toggle("Remember Me", _rememberMe);
                }
                if (_page == LoginPage.Register)
                    _confirmPassword = MetaverseEditorUtils.TextField("Confirm Password", _confirmPassword, true);
            });

            GUILayout.FlexibleSpace();

            MetaverseEditorUtils.Info(_successMessage);

            MetaverseEditorUtils.Box(() =>
            {
                if (_page == LoginPage.LogIn && GUILayout.Button("Log In"))
                {
                    BeginLogin();
                }

                GUILayout.Space(10);

                if (_page == LoginPage.LogIn && GUILayout.Button("Don't have an account?", EditorStyles.linkLabel))
                {
                    BeginRegister();
                }
                else if (_page == LoginPage.Register && GUILayout.Button("Register"))
                {
                    BeginRegister();
                }
                
                if (_page == LoginPage.LogIn && GUILayout.Button("Forgot password?", EditorStyles.linkLabel))
                {
                    BeginForgotPassword();
                }
                else if (_page == LoginPage.ResetPassword && GUILayout.Button("Send Email"))
                {
                    BeginForgotPassword();
                }
                
                if (_page is LoginPage.Register or LoginPage.ResetPassword && GUILayout.Button("< Back to Log In"))
                {
                    _page = LoginPage.LogIn;
                }
            });
        }

        private static void BeginForgotPassword()
        {
            if (_page != LoginPage.ResetPassword)
            {
                _page = LoginPage.ResetPassword;
                _errorMessage = null;
            }
            else
            {
                _makingRequest = true;

                MetaverseProgram.ApiClient.Account.RequestResetPasswordTokenAsync(new SystemUserRequestResetPasswordTokenForm
                {
                    Email = _email,
                })
                .ResponseThen(() =>
                {
                    _successMessage = "An email was sent to '" + _email + "' for a password reset.";
                    _makingRequest = false;

                }, LoginError);
            }
        }

        private static void BeginRegister()
        {
            if (_page != LoginPage.Register)
            {
                _page = LoginPage.Register;
                _errorMessage = null;
            }
            else
            {
                _makingRequest = true;

                MetaverseProgram.ApiClient.Account.RegisterAsync(new RegisterSystemUserForm
                {
                    UserName = _username,
                    Password = _password,
                    Email = _email,
                    ConfirmPassword = _confirmPassword,
                })
                .ResponseThen(() =>
                {
                    _successMessage = "A confirmation email was sent to '" + _email + "'. Please confirm your email to complete your account registration.";
                    _makingRequest = false;

                }, LoginError);
            }
        }

        private static void BeginLogin()
        {
            _makingRequest = true;

            MetaverseProgram.ApiClient.Account.PasswordSignInAsync(
                new GenerateSystemUserTokenForm
                {
                    UserNameOrEmail = _username,
                    Password = _password,
                    RememberMe = _rememberMe,
                })
            .ResponseThen(_ =>
            {
                _makingRequest = false;
                _errorMessage = null; 

            }, LoginError);
        }

        private static void LoginError(object e)
        {
            _errorMessage = e.ToString();
            _makingRequest = false;
        }
    }
}
