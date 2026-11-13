export declare const DEPENDENCY_FOLDERS: string[];
export declare const DEPENDENCY_STAMP: string;
export declare const NPM_CI: string;
export declare function stampDependencies(tree: string, folder: string): void;
export declare function isStamped(tree: string, folder: string): boolean;
export declare function dependencyState(tree: string, folder: string): 'current' | 'missing' | 'stale' | 'linked';
export declare function staleDependencies(tree: string): string[];
