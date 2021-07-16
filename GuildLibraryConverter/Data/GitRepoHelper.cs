using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GuildLibraryConverter.Data
{
    class GitUserInfo
    {
        public string UserName { get; set; }
        public string Email { get; set; }
        public string UploadLogin { get; set; }
        public string UploadPassword { get; set; }
    }

    static class GitRepoHelper
    {
        private static Signature GetSignature(GitUserInfo user)
        {
            return new Signature(user.UserName, user.Email, DateTimeOffset.Now);
        }

        public static void Clone(string url, string dir, GitUserInfo user)
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
            var repoPath = Repository.Init(dir, dir);
            using var repo = new Repository(repoPath);
            var remote = repo.Network.Remotes.Add("origin", url);
            var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
            Commands.Fetch(repo, remote.Name, refSpecs, new FetchOptions()
            {
                CredentialsProvider = (_url, _user, _cred) =>
                  new UsernamePasswordCredentials { Username = user.UploadLogin, Password = user.UploadPassword },
            }, string.Empty);
            Commands.Checkout(repo, repo.Branches["refs/remotes/origin/master"]);
            var master = repo.CreateBranch("master");
            Commands.Checkout(repo, master);
            repo.Branches.Update(master, b => b.TrackedBranch = "refs/remotes/origin/master");
        }

        public static void Pull(string path, GitUserInfo user)
        {
            using var repo = new Repository(path);
            Commands.Pull(repo, GetSignature(user), new PullOptions()
            {
                FetchOptions = new()
                {
                    CredentialsProvider = (_url, _user, _cred) =>
                      new UsernamePasswordCredentials { Username = user.UploadLogin, Password = user.UploadPassword },
                },
            });
        }

        public static void Commit(string path, GitUserInfo user)
        {
            using var repo = new Repository(path);
            Commands.Stage(repo, "*");
            var sig = GetSignature(user);
            repo.Commit("Update library repo.", sig, sig);
        }

        public static void Push(string path, GitUserInfo user)
        {
            using var repo = new Repository(path);
            var remote = repo.Network.Remotes["origin"];
            var options = new PushOptions()
            {
                CredentialsProvider = (_url, _user, _cred) =>
                   new UsernamePasswordCredentials { Username = user.UploadLogin, Password = user.UploadPassword },
            };
            repo.Network.Push(remote, @"refs/heads/master", options);
        }
    }
}
