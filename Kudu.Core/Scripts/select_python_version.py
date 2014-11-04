# Launch this script using the python.exe that is in the PATH

# Detects the version of Python to use:
#   Version specified in runtime.txt in deployment source folder
#   Version from site configuration -- currently executing Python (in PATH)

# Writes the detected version information to files in deployment temp folder:
#   __PYTHON_RUNTIME.tmp
#   __PYTHON_VER.tmp
#   __PYTHON_EXE.tmp
#   __PYTHON_ENV_MODULE.tmp

# Returns an error for unexpected or unsupported Python version

from os import path, getenv
import sys

# Update this list when adding support for a new version of Python
supported_runtime_list = [
    ('python-2.7', '2.7', 'python27', 'virtualenv'),
    ('python-3.4', '3.4', 'python34', 'venv'),
]

system_drive = getenv('SYSTEMDRIVE')

def write_temp(tmp_folder, name, val):
    with open(path.join(tmp_folder, '{0}.tmp'.format(name)), 'w') as f:
        f.write(val)

def write_runtime_info(tmp_folder, runtime_text):
    for runtime, ver, folder, env_module in supported_runtime_list:
        if runtime_text .lower().startswith(runtime):
            write_temp(tmp_folder, '__PYTHON_RUNTIME', runtime)
            write_temp(tmp_folder, '__PYTHON_VER', ver)
            write_temp(tmp_folder, '__PYTHON_EXE', path.join(system_drive, '\\', folder, 'python.exe'))
            write_temp(tmp_folder, '__PYTHON_ENV_MODULE', env_module)
            return True
    return False

def print_result(detected, runtime_text):
    if detected:
        print('Detected {0}'.format(runtime_text))
    else:
        print('Unsupported runtime: {0}'.format(runtime_text))
        print('Supported runtime values are:')
        for runtime, _, _, _ in supported_runtime_list:
            print(runtime)

if __name__ == '__main__':
    repo_folder = sys.argv[1]
    site_root_folder = sys.argv[2]
    tmp_folder = sys.argv[3]

    detected = True

    runtime_path = path.join(repo_folder, 'runtime.txt')
    if path.isfile(runtime_path):
        print('Detecting Python runtime from runtime.txt')
        with open(runtime_path, 'r') as runtime_file:
            runtime_text = runtime_file.read()
            detected = write_runtime_info(tmp_folder, runtime_text)
    else:
        print('Detecting Python runtime from site configuration')
        runtime_text = 'python-{0}.{1}'.format(sys.version_info.major, sys.version_info.minor)
        detected = write_runtime_info(tmp_folder, runtime_text)

    print_result(detected, runtime_text)

    if not detected:
        sys.exit(1)
