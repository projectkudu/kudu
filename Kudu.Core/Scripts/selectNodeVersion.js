var path = require('path'),
    fs = require('fs'),
    semver = require('./semver.js');

var existsSync = fs.existsSync || path.existsSync;

function flushAndExit(code) {
    var exiting;
    process.on('exit', function () {
        if (exiting)
            return;

        exiting = true;
        process.exit(code);
    });
};


function getDefaultNpmPath(npmRootPath, nodeVersionPath) {
    var npmLinkPath = path.resolve(nodeVersionPath, 'npm.txt');
    
    // Determine if there's a link to npm at the node path
    if (!existsSync(npmLinkPath)) {
        return;
    }
    
    var npmVersion = fs.readFileSync(npmLinkPath, 'utf8'),
        npmPath = path.resolve(npmRootPath, npmVersion, 'bin', 'npm-cli.js');

    if (!existsSync(npmPath)) {
        throw new Error('Unable to locate npm version ' + npmVersion + ' at ' + npmPath);
    }
    return npmPath;
}

function getNpmPath(npmRootPath, json) {
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
    
    return path.resolve(npmRootPath, npmVersion, 'bin', 'npm-cli.js');
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

function createIisNodeWebConfigIfNeeded(repoPath, wwwrootPath) {
    // Check if web.config exists in the 'repository', if not generate it in 'wwwroot'
    var webConfigRepoPath = path.join(repoPath, 'web.config');
    var webConfigWwwRootPath = path.join(wwwrootPath, 'web.config');

    if (!existsSync(webConfigRepoPath)) {
        var nodeStartFilePath = getNodeStartFile(repoPath);
        if (!nodeStartFilePath) {
            console.log('Missing server.js/app.js files, web.config is not generated');
            return;
        }

        var iisNodeConfigTemplatePath = path.join(__dirname, 'iisnode.config.template');
        var webConfigContent = fs.readFileSync(iisNodeConfigTemplatePath, 'utf8');
        webConfigContent = webConfigContent.replace(/{NodeStartFile}/g, nodeStartFilePath);

        fs.writeFileSync(webConfigWwwRootPath, webConfigContent, 'utf8');
    }
}

function getNodeStartFile(sitePath) {
    var nodeStartFiles = ['server.js', 'app.js'];

    for (var i in nodeStartFiles) {
        var nodeStartFilePath = path.join(sitePath, nodeStartFiles[i]);
        if (existsSync(nodeStartFilePath)) {
            return nodeStartFiles[i];
        }
    }

    return null;
}

// Determine the installation location of node.js and iisnode

var programFilesDir = process.env['programfiles(x86)'] || process.env['programfiles'],
    nodejsDir = path.resolve(programFilesDir, 'nodejs'),
    npmDir = path.resolve(programFilesDir, 'npm');

if (!existsSync(nodejsDir))
    throw new Error('Unable to locate node.js installation directory at ' + nodejsDir);

var interceptorJs = path.resolve(process.env['programfiles(x86)'], 'iisnode', 'interceptor.js');
if (!existsSync(interceptorJs)) {
    interceptorJs = path.resolve(process.env['programfiles'], 'iisnode', 'interceptor.js');
    if (!existsSync(interceptorJs))
        throw new Error('Unable to locate iisnode installation directory with interceptor.js file');
}

// Validate input parameters

var repo = process.argv[2];
var wwwroot = process.argv[3];
var tempDir = process.argv[4];
if (!existsSync(wwwroot) || !existsSync(repo) || (tempDir && !existsSync(tempDir)))
    throw new Error('Usage: node.exe selectNodeVersion.js <path_to_repo> <path_to_wwwroot> [path_to_temp]');

// If the web.config file does not exit in the repo, use a default one that is specific for node on IIS in Azure
createIisNodeWebConfigIfNeeded(repo, wwwroot);

// If the iinode.yml file does not exit in the repo but exists in wwwroot, remove it from wwwroot 
// to prevent side-effects of previous deployments

var iisnodeYml = path.resolve(repo, 'iisnode.yml');
var wwwrootIisnodeYml = path.resolve(wwwroot, 'iisnode.yml');
if (!existsSync(iisnodeYml) && existsSync(wwwrootIisnodeYml)) {
    fs.unlinkSync(wwwrootIisnodeYml);
}

// If the package.json file is not included with the application 
// or if it does not specify node.js version constraints, exit this script. 
// This will cause the default node.js version to be used to run the application, 
// unless an explicit nodeProcessCommandLine setting is present

var packageJson = path.resolve(repo, 'package.json');
if (!existsSync(packageJson)) {
    console.log('The package.json file is not present.');
    console.log('The node.js application will run with the default node.js version '
        + process.versions.node + '.');
    return flushAndExit(0);
}

var json = JSON.parse(fs.readFileSync(packageJson, 'utf8'));
if (typeof json !== 'object' || typeof json.engines !== 'object' || typeof json.engines.node !== 'string') {
    // Attempt to read the pinned node version or fallback to the version of the executing node.exe.
    if (process.env['WEBSITE_NODE_DEFAULT_VERSION']) {
        var nodeVersion = process.env['WEBSITE_NODE_DEFAULT_VERSION'],
	        nodeVersionPath = path.resolve(nodejsDir, nodeVersion),
            nodeExePath = path.resolve(nodeVersionPath, 'node.exe'),
            npmPath = getDefaultNpmPath(npmDir, nodeVersionPath);

        saveNodePaths(tempDir, nodeExePath, npmPath);
    }
    nodeVersion = nodeVersion || process.versions.node;

    console.log('The package.json file does not specify node.js engine version constraints.');
    console.log('The node.js application will run with the default node.js version '
        + nodeVersion + '.');
    return flushAndExit(0);
}

// If the iisnode.yml included with the application explicitly specifies the
// nodeProcessCommandLine, exit this script. The presence of nodeProcessCommandLine
// deactivates automatic version selection.

var yml = '';
if (existsSync(iisnodeYml)) {
    yml = fs.readFileSync(iisnodeYml, 'utf8');
    if (yml.match(/^ *nodeProcessCommandLine *:/m)) {
        console.log('The iisnode.yml file explicitly sets nodeProcessCommandLine. '
            + 'Automatic node.js version selection is turned off.');
        return flushAndExit(0);
    }
}

// Determine the set of node.js versions available on the platform

var versions = [];
fs.readdirSync(nodejsDir).forEach(function (dir) {
    if (dir.match(/^\d+\.\d+\.\d+$/) && existsSync(path.resolve(nodejsDir, dir, 'node.exe')))
        versions.push(dir);
});

console.log('Node.js versions available on the platform are: ' + versions.join(', ') + '.');

// Calculate actual node.js version to use for the application as the maximum available version
// that satisfies the version constraints from package.json.

var nodeVersion = semver.maxSatisfying(versions, json.engines.node);
if (!nodeVersion) {
    console.error('No available node.js version matches application\'s version constraint of \''
        + json.engines.node + '\'.');
    console.error('Use package.json to choose one of the available versions.');
    return flushAndExit(-1);
}

console.log('Selected node.js version ' + nodeVersion + '. Use package.json file to choose a different version.');

var nodeVersionPath = path.resolve(nodejsDir, nodeVersion),
    nodeExePath = path.resolve(nodeVersionPath, 'node.exe'),
    npmPath;
try {
    npmPath = getNpmPath(npmDir, json) || getDefaultNpmPath(npmDir, nodeVersionPath);
} catch (ex) {
    console.error(ex.message);
    return flushAndExit(-1);
}

// Save the version information to iisnode.yml in the wwwroot directory

if (yml !== '')
    yml += '\r\n';

yml += 'nodeProcessCommandLine: "' + nodeExePath + '"';
fs.writeFileSync(wwwrootIisnodeYml, yml);

// Save the node version in a temporary path for kudu service usage
saveNodePaths(tempDir, nodeExePath, npmPath);

