var path = require('path'),
    fs = require('fs'),
    semver = require('./semver.js');

var existsSync = fs.existsSync || path.existsSync;

function flushAndExit(code) {
    var exiting;
    process.on('exit', function () {
        if (exiting) {
            return;
        }
        exiting = true;
        process.exit(code);
    });
}

function resolveNpmPath(npmRootPath, npmVersion) {
    if (!npmVersion) {
        return;
    }

    var npmPath = path.resolve(npmRootPath, npmVersion, 'node_modules', 'npm', 'bin', 'npm-cli.js');
    if (!existsSync(npmPath)) {
        // Try resolving it using the old npm layout
        npmPath = path.resolve(npmRootPath, npmVersion, 'bin', 'npm-cli.js');
        if (!existsSync(npmPath)) {
            throw new Error('Unable to locate npm version ' + npmVersion);
        }
    }

    return npmPath;
}


function getDefaultNpmVersion(nodeVersionPath) {
    var appSettingNpmVersion = process.env.WEBSITE_NPM_DEFAULT_VERSION;

    // extract the current node version from the path
    var nodePathSplited = nodeVersionPath.split(path.sep).filter(function(e) { return e; });
    var currentNodeVersion = nodePathSplited[nodePathSplited.length - 1];

    if (appSettingNpmVersion) {
        return appSettingNpmVersion;
    } else if (currentNodeVersion === '4.1.2') {
        // This is to preserve parity with kudu's behavior to fix issues with ASP.NET 5 use of npm
        return '3.3.6';
    } else {
        var npmLinkPath = path.resolve(nodeVersionPath, 'npm.txt');
        // Determine if there's a link to npm at the node path
        if (!existsSync(npmLinkPath)) {
            return null;
        }
        var npmVersion = fs.readFileSync(npmLinkPath, 'utf8').trim();
        return npmVersion;
    }
}

function getNpmVersionFromJson(npmRootPath, json) {
    if (typeof json.engines.npm !== 'string') {
        return;
    }

    var versions = [];
    fs.readdirSync(npmRootPath).forEach(function (dir) {
        versions.push(dir);
    });

    var npmVersion = semver.maxSatisfying(versions, json.engines.npm);
    if (!npmVersion) {
        var errorMsg = 'No available npm version matches application\'s version constraint of \''
                        + json.engines.npm + '\'. Use package.json to choose one of the following versions: '
                        + versions.join(', ') + '.';
        throw new Error(errorMsg);
    }
    return npmVersion;
}

function saveNodePaths(tempDir, nodeExePath, npmPath) {
    if (!tempDir) {
        return;
    }
    var nodeTmpFile = path.resolve(tempDir, '__nodeVersion.tmp'),
        npmTempFile = path.resolve(tempDir, '__npmVersion.tmp');

    fs.writeFileSync(nodeTmpFile, nodeExePath);
    if (npmPath) {
        fs.writeFileSync(npmTempFile, npmPath);
    }
}

function getNodeDefaultStartFile(sitePath) {
    var nodeStartFiles = ['server.js', 'app.js'];

    for (var i = 0; i < nodeStartFiles.length; i++) {
        var nodeStartFilePath = path.join(sitePath, nodeStartFiles[i]);
        if (existsSync(nodeStartFilePath)) {
            return nodeStartFiles[i];
        }
    }

    return null;
}

// Determine the set of node.js versions available on the platform
function getInstalledNodeVersions(nodeJsDir) {
    var versions = [];
    fs.readdirSync(nodejsDir).forEach(function (dir) {
        if (process.platform === "linux") {
            if (dir.match(/^\d+\.\d+\.\d+$/) && existsSync(path.resolve(nodejsDir, dir, "bin", "node"))) {
                versions.push(dir);
            }
        } else {
            if (dir.match(/^\d+\.\d+\.\d+$/) && existsSync(path.resolve(nodejsDir, dir, 'node.exe'))) {
                versions.push(dir);
            }
        }
    });

    console.log('Node.js versions available on the platform are: ' + versions.sort(semver.compare).join(', ') + '.');

    return versions;
}

// Determine the set of NPM versions available on the platform
function getInstalledNpmVersions(npmDir) {
    var versions = [];
    fs.readdirSync(npmDir).forEach(function (dir) {
        if (process.platform === "linux") {
            if (dir.match(/^\d+\.\d+\.\d+$/) && existsSync(path.resolve(npmDir, dir, 'node_modules/npm/bin/npm-cli.js'))) {
                versions.push(dir);
            }
        } else {
            if (dir.match(/^\d+\.\d+\.\d+$/) && existsSync(path.resolve(npmDir, dir, 'npm.cmd'))) {
                versions.push(dir);
            }
        }
    });

    console.log('NPM versions available on the platform are: ' + versions.sort(semver.compare).join(', ') + '.');

    return versions;
}

// Determine the installation location of node.js and iisnode
var programFilesDir, nodejsDir, npmRootPath;

if (process.platform === "linux") {
    nodejsDir = "/opt/nodejs";
    npmRootPath = "/opt/npm";
} else {
    programFilesDir = process.env['programfiles(x86)'] || process.env.programfiles;
    nodejsDir = path.resolve(programFilesDir, 'nodejs');
    npmRootPath = path.resolve(programFilesDir, 'npm');

    var interceptorJs = path.resolve(process.env['programfiles(x86)'], 'iisnode', 'interceptor.js');
    if (!existsSync(interceptorJs)) {
        interceptorJs = path.resolve(process.env.programfiles, 'iisnode', 'interceptor.js');
        if (!existsSync(interceptorJs)) {
            throw new Error('Unable to locate iisnode installation directory with interceptor.js file');
        }
    }
}

if (!existsSync(nodejsDir)) {
    throw new Error('Unable to locate node.js installation directory at ' + nodejsDir);
}

// Validate input parameters

var repo = process.argv[2];
var wwwroot = process.argv[3];
var tempDir = process.argv[4];
if (!existsSync(wwwroot) || !existsSync(repo) || (tempDir && !existsSync(tempDir))) {
    throw new Error('Usage: node.exe selectNodeVersion.js <path_to_repo> <path_to_wwwroot> [path_to_temp]');
}

var packageJson = path.resolve(repo, 'package.json'),
    json = existsSync(packageJson) && JSON.parse(fs.readFileSync(packageJson, 'utf8'));

if (process.platform === "linux") {
    try {
        // Select Node version
        console.log('Detecting node version spec...');
        var nodeVersionSpec;
        if (typeof json == 'object' && typeof json.engines == 'object' && typeof json.engines.node == 'string') {
            nodeVersionSpec = json.engines.node;
            console.log('Using package.json engines.node value: ' + nodeVersionSpec);
        }
        else if (process.env.WEBSITE_NODE_DEFAULT_VERSION) {
            nodeVersionSpec = process.env.WEBSITE_NODE_DEFAULT_VERSION;
            console.log('Using appsetting WEBSITE_NODE_DEFAULT_VERSION value: ' + nodeVersionSpec);
        }
        else {
            nodeVersionSpec = process.versions.node;
            console.log('Using default version: ' + nodeVersionSpec)
        }

        var installedNodeVersions = getInstalledNodeVersions(nodejsDir);
        var nodeResolvedVersion = semver.maxSatisfying(installedNodeVersions, nodeVersionSpec);
        
        if (nodeResolvedVersion) {
            console.log('Resolved to version ' + nodeResolvedVersion);
        }
        else {
            console.log('Could not resolve node version. Deployment will proceed with default versions of node and npm.');
            return;
        }

        var nodePath = path.resolve(nodejsDir, nodeResolvedVersion, "bin/node");      

        // Select NPM version
        var npmVersionSpec;
        var npmVersionSpecFileForResolvedNodeVersion = path.resolve(nodePath, '../../npm.txt');
        console.log('Detecting npm version spec...');
        if (typeof json == 'object' && typeof json.engines == 'object' && typeof json.engines.npm == 'string') {
            npmVersionSpec = json.engines.npm;
            console.log('Using package.json engines.npm value: ' + npmVersionSpec);
        }
        else if (process.env.WEBSITE_NPM_DEFAULT_VERSION) {
            npmVersionSpec = process.env.WEBSITE_NPM_DEFAULT_VERSION;
            console.log('Using appsetting WEBSITE_NPM_DEFAULT_VERSION value: ' + npmVersionSpec);
        }
        else {
            npmVersionSpec = fs.readFileSync(npmVersionSpecFileForResolvedNodeVersion, 'utf8').trim();
            console.log('Using default for node ' + nodeResolvedVersion + ': ' + npmVersionSpec);
        }

        var installedNpmVersions = getInstalledNpmVersions(npmRootPath);
        var npmResolvedVersion = semver.maxSatisfying(installedNpmVersions, npmVersionSpec);
        if (npmResolvedVersion) {
            console.log('Resolved to version ' + npmResolvedVersion);
        }
        else
        {
            console.log('Could not resolve npm version. Deployment will proceed with default versions of node and npm.');
            return;
        }

        var npmPath = path.resolve(npmRootPath, npmResolvedVersion, 'node_modules/npm/bin/npm-cli.js');

        saveNodePaths(tempDir, nodePath, npmPath);
    } catch (ex) {
        console.error(ex.message);
        flushAndExit(-1);
    }
}
else {
    // If the web.config file does not exist in the repo, use a default one that is specific for node on IIS in Azure, 
    // and generate it in 'wwwroot'
    // Obtain the start script from package.json or seach for app.js/server.js at the root of the repository.
    var nodeStartFilePath = (function createIisNodeWebConfigIfNeeded() {
        var webConfigRepoPath = path.join(repo, 'web.config'),
            webConfigWwwRootPath = path.join(wwwroot, 'web.config'),
            nodeStartFilePath = null;

        // Check for {"scripts": {"start": < startupCommand > } } exists
        if (typeof json === 'object' && typeof json.scripts === 'object' && typeof json.scripts.start === 'string') {
            var startupCommand = json.scripts.start;
            var defaultNode = "node ";
            if (startupCommand.length > defaultNode.length && startupCommand.slice(0, defaultNode.length) === defaultNode) {
                var startFile = path.resolve(repo, startupCommand.slice(defaultNode.length));
                var startFileJs = path.resolve(repo, startupCommand.slice(defaultNode.length) + ".js");
                if (existsSync(startFile)) {
                    nodeStartFilePath = path.relative(repo, startFile);
                } else if (existsSync(startFileJs)) {
                    nodeStartFilePath = path.relative(repo, startFileJs);
                }
                if (nodeStartFilePath) {
                    // iisnode requires forward-slash in paths
                    nodeStartFilePath = nodeStartFilePath.replace(/\\/g, '/');
                    console.log('Using start-up script ' + nodeStartFilePath + ' from package.json.');
                } else {
                    console.log('Start script "' + startupCommand.slice(defaultNode.length) + '" from package.json is not found.');
                }
            } else {
                console.error('Invalid start-up command "' + startupCommand + '" in package.json. Please use the format "node <script relative path>".');
            }
        }

        if (!nodeStartFilePath) {
            console.log('Looking for app.js/server.js under site root.');
            nodeStartFilePath = getNodeDefaultStartFile(repo);
            if (!nodeStartFilePath) {
                console.error('Missing server.js/app.js files, web.config is not generated');
                return nodeStartFilePath;
            }
            console.log('Using start-up script ' + nodeStartFilePath);
        }

        if (!existsSync(webConfigRepoPath)) {
            var iisNodeConfigTemplatePath = path.join(__dirname, 'iisnode.config.template');
            var webConfigContent = fs.readFileSync(iisNodeConfigTemplatePath, 'utf8');
            webConfigContent = webConfigContent.replace(/\{NodeStartFile\}/g, nodeStartFilePath);

            fs.writeFileSync(webConfigWwwRootPath, webConfigContent, 'utf8');

            console.log('Generated web.config.');
        }

        return nodeStartFilePath;
    })();

    // The directory of the start up script.
    var nodeStartDirectory = (function () {
        if (!nodeStartFilePath) {
            return "";
        }

        var index = nodeStartFilePath.lastIndexOf("/");
        if (index === -1) {
            return "";
        } else {
            return nodeStartFilePath.slice(0, index);
        }
    })();

    // If the iinode.yml file does not exist in the repo but exists in wwwroot, remove it from wwwroot 
    // to prevent side-effects of previous deployments
    var repoIisnodeYml = path.resolve(repo, nodeStartDirectory, 'iisnode.yml');
    var siteIisnodeYml = path.resolve(wwwroot, nodeStartDirectory, 'iisnode.yml');
    if (!existsSync(repoIisnodeYml) && existsSync(siteIisnodeYml)) {
        fs.unlinkSync(siteIisnodeYml);
    }

    try {
        var nodeVersion = process.env.WEBSITE_NODE_DEFAULT_VERSION || process.versions.node,
            npmVersion = null,
            yml = existsSync(repoIisnodeYml) ? fs.readFileSync(repoIisnodeYml, 'utf8') : '',
            shouldUpdateIisNodeYml = false;

        if (yml.match(/^ *nodeProcessCommandLine *:/m)) {
            // If the iisnode.yml included with the application explicitly specifies the
            // nodeProcessCommandLine, exit this script. The presence of nodeProcessCommandLine
            // deactivates automatic version selection.
            console.log('The iisnode.yml file explicitly sets nodeProcessCommandLine. ' +
                'Automatic node.js version selection is turned off.');
        } else {
            // If the package.json file is not included with the application 
            // or if it does not specify node.js version constraints, use WEBSITE_NODE_DEFAULT_VERSION. 
            if (typeof json !== 'object' || typeof json.engines !== 'object' || typeof json.engines.node !== 'string') {
                // Attempt to read the pinned node version or fallback to the version of the executing node.exe.
                console.log('The package.json file does not specify node.js engine version constraints.');
                console.log('The node.js application will run with the default node.js version '
                    + nodeVersion + '.');
            } else {
                var versions = getInstalledNodeVersions(nodejsDir);

                // Calculate actual node.js version to use for the application as the maximum available version
                // that satisfies the version constraints from package.json.
                nodeVersion = semver.maxSatisfying(versions, json.engines.node);
                if (!nodeVersion) {
                    throw new Error('No available node.js version matches application\'s version constraint of \''
                        + json.engines.node + '\'. Use package.json to choose one of the available versions.');
                }

                console.log('Selected node.js version ' + nodeVersion + '. Use package.json file to choose a different version.');
                npmVersion = getNpmVersionFromJson(npmRootPath, json);
                shouldUpdateIisNodeYml = true;
            }
        }

        var nodeVersionPath = path.resolve(nodejsDir, nodeVersion),
            nodeExePath = path.resolve(nodeVersionPath, 'node.exe'),
            npmPath;

        npmVersion = npmVersion || getDefaultNpmVersion(nodeVersionPath);
        npmPath = resolveNpmPath(npmRootPath, npmVersion)

        console.log("Selected npm version " + npmVersion);

        // Save the node version in a temporary path for kudu service usage
        if (existsSync(nodeExePath) && existsSync(npmPath)) {
            saveNodePaths(tempDir, nodeExePath, npmPath);
        } else {
            console.log("One or more of the selected node/npm paths do not exist.");
        }

        if (shouldUpdateIisNodeYml) {
            // Save the version information to iisnode.yml in the start script directory

            if (yml !== '') {
                yml += '\r\n';
            }

            yml += 'nodeProcessCommandLine: "' + nodeExePath + '"';

            console.log('Updating iisnode.yml at ' + siteIisnodeYml);

            fs.writeFileSync(siteIisnodeYml, yml);
        }
    } catch (ex) {
        console.error(ex.message);
        flushAndExit(-1);
    }
}