import { useQuery } from '@tanstack/react-query';
import { fetchDirectories } from '@/services/directories.api';

export function useDirectoriesQuery(path?: string) {
  return useQuery({
    queryKey: ['directories', path],
    queryFn: () => fetchDirectories(path),
    staleTime: 1000 * 60, // 1 minute
  });
}
