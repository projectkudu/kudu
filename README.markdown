### Kudu

Kudu is the engine behind [git deployments in Azure Web Sites](https://www.windowsazure.com/en-us/develop/nodejs/common-tasks/publishing-with-git/). It can also run outside of Azure.

The Kudu is an [Outercurve Foundation](http://www.outercurve.org/) project.


### Documentation

See the [documentation](https://github.com/projectkudu/kudu/wiki)

### License

[Apache License 2.0](https://github.com/projectkudu/kudu/blob/master/LICENSE.txt)

### Questions?

You can use the [forum](http://social.msdn.microsoft.com/Forums/en-US/azuregit/threads), chat on [JabbR](https://jabbr.net/#/rooms/kudu), or open issues in this repository.


### Git workFlow 

__Working on your changes__

1. Start by getting the latest code

        git pull 

1. Ensure that you can [build](http://blog.davidebbo.com/2012/06/developing-kudu-locally-and-on-azure.html) the code 
and [run the tests](https://github.com/projectkudu/kudu/wiki/Running-tests). If this is your first time 
contributing code, familiarize yourself with our [coding guidelines](http://aspnetwebstack.codeplex.com/wikipage?title=CodingConventions)

1. Create a topic branch 

        git checkout -b <topic branch name>

1. Make your changes and test. Commit often with meaningful commit messages:

        git add .
        git commit

__Integrating your changes__

We prefer the rebase model for merging changes. 

1. Start by closing Visual Studio. 
1. Use `fetch` to get the latest from GitHub.

        git fetch origin

1. Rebase your changes on to master

        git rebase origin/master

1. Resolve any merge conflicts, commit your changes and run the tests again

1. Merge the changes into master

        git checkout master
        git merge <topic branch>

1. Push your changes to the server. If you are not a contributor to the project, you will need to send a [pull request](https://help.github.com/articles/using-pull-requests). 
If not, proceed to push to master

        git push origin

1. Remove the topic branch

        git branch -d <topic branch>


[![Outercurve Foundation](http://www.outercurve.org/Portals/0/Skins/CodePlex_NEW/images/footer-logo.jpg)](http://www.outercurve.org/)
