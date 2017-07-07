import { Column } from 'react-data-grid';
import { WorkerInfo } from './DataService';
import axios from 'axios';
import { AxiosResponse } from 'axios';

export interface WorkerInfo {
    id: string;
    stampName: string;
    workerName: string;
    loadFactor: string;
    lastModifiedTimeUtc: string;
    isManager: string;
    isStale: string;
}


export function isWorkerInfo(obj: any): obj is WorkerInfo {
    return 'stampName' in obj;
}

type Unit = 'unit';

//const rootPath = 'https://scaleuxreact.azurewebsites.net/';
const rootPath = '/';
const workersPath = `${rootPath}api/workers`;
const workerPath = `${workersPath}/{id}`;
const addWorkerPath = `${workerPath}/add`;
const pingWorkerPath = `${workerPath}/ping`;

export class DataService {
    static getWorkersColumns(): Column[] {
        return [
            {
                key: 'stampName',
                name: 'StampName',
            }, {
                key: 'workerName',
                name: 'Worker Name',
            }, {
                key: 'loadFactor',
                name: 'Load Factor',
            }, {
                key: 'lastModifiedTimeUtc',
                name: 'Last Modified Time Utc',
            }, {
                key: 'isManager',
                name: 'Is Manager',
            }, {
                key: 'isStale',
                name: 'Is Stale',
            }
        ];
    }

    static async getWorkers(): Promise<WorkerInfo[] | null> {
        try {
            const response = await axios.get(workersPath);
            return (response.data as any[]).map(o => {
                o.isManager = o.isManager.toString();
                o.isStale = o.isStale.toString();
                return o;
            }) as WorkerInfo[];
        } catch (e) {
            return null;
        }
    }

    static async addWorker(managerId: string): Promise<WorkerInfo | AxiosResponse> {
        try {
            const response = await axios.post(addWorkerPath.replace('{id}', managerId));
            return response.data as WorkerInfo
        } catch (e) {
            return e;
        }
    }

    static async pingWorker(workerId: string): Promise<WorkerInfo | AxiosResponse> {
        try {
            const response = await axios.post(pingWorkerPath.replace('{id}', workerId));
            return response.data as WorkerInfo;
        } catch (e) {
            return e;
        }
    }

    static async removeWorker(workerId: string): Promise<Unit | AxiosResponse> {
        try {
            await axios.delete(workerPath.replace('{id}', workerId));
            return 'unit'
        } catch (e) {
            return e;
        }
    }
}